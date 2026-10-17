using System.Net.Http.Json;
using Kamus.Infrastructure.MultiTenancy;
using Kamus.SharedKernel.Catalogo;
using Kamus.SharedKernel.Estoque;
using Kamus.SharedKernel.Financeiro;
using Microsoft.Extensions.DependencyInjection;
using static Kamus.IntegrationTests.Estoque.ApiDoEstoque;

namespace Kamus.IntegrationTests.Contratos;

/// <summary>O que o módulo de vendas usa dos outros módulos (pelo SharedKernel): baixa de estoque, contas a receber e vendedores.</summary>
public sealed class ContratosDeVendaTests : IAsyncDisposable
{
    private static readonly Guid TenantDemo = Guid.Parse("d0000000-0000-0000-0000-000000000001");
    private readonly Infra.KamusApiFactory _api = NovaApi();

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    private sealed record EmpresaDto(Guid Id, string NomeFantasia);
    private sealed record SessaoDto(List<EmpresaDto> Empresas);
    private sealed record CelulaDto(Guid SkuId, int Quantidade);
    private sealed record MatrizDto(List<CelulaDto> Celulas);
    private sealed record MovimentoDto(string Tipo, int Quantidade, string? Documento);
    private sealed record ContaDto(Guid Id, string Nome, decimal Saldo);
    private sealed record LinhaDto(string Descricao, DateOnly Vencimento, decimal Valor, decimal Saldo, string Situacao);
    private sealed record ListaDto(List<LinhaDto> Itens);

    private async Task<Guid> EmpresaCentro()
    {
        var login = await _api.CreateClient().PostAsJsonAsync(U("/api/auth/login"), new { email = "admin@kamus.local", senha = Senha }, Ct);
        return (await login.Content.ReadFromJsonAsync<SessaoDto>(Ct))!.Empresas.Single(e => e.NomeFantasia == "Kamus Moda Centro").Id;
    }

    private AsyncServiceScope Escopo()
    {
        var escopo = _api.Services.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<TenantContext>().Definir(TenantDemo);
        return escopo;
    }

    private static async Task<int> Saldo(HttpClient client, Guid produtoId, Guid skuId) =>
        (await client.GetFromJsonAsync<MatrizDto>(U($"/api/estoque/posicao/{produtoId}"), Ct))!.Celulas.Single(c => c.SkuId == skuId).Quantidade;

    [Fact]
    public async Task Estoque_VendaBaixaComCustoMedio_NegativoSoSePermitido_EstornoDevolveUmaVez()
    {
        var client = await Entrar(_api);
        var empresa = await EmpresaCentro();
        var camiseta = await CriarCamiseta(client);
        var loja = (await Lojas(client)).Single();
        var azulM = Sku(camiseta, "Azul", "M").Id;
        await Entrada(client, loja.Id, new { skuId = azulM, quantidade = 2, custoUnitario = 20m });

        await using var escopo = Escopo();
        var estoque = escopo.ServiceProvider.GetRequiredService<IMovimentacaoDeEstoque>();

        var recusada = await Should.ThrowAsync<SaidaDeEstoqueRecusadaException>(() =>
            estoque.RegistrarVendaAsync(new SaidaDeEstoque(empresa, loja.Id, "Venda 1", [new ItemDeSaida(azulM, 3)], PermitirNegativo: false), Ct));
        recusada.Message.ShouldContain("há 2");
        var saida = await estoque.RegistrarVendaAsync(new SaidaDeEstoque(empresa, loja.Id, "Venda 1", [new ItemDeSaida(azulM, 3)], PermitirNegativo: true), Ct);

        saida.Itens.Single().ShouldBe(new ItemSaido(azulM, 3, 20m, -1));
        (await Saldo(client, camiseta.Id, azulM)).ShouldBe(-1);
        (await client.GetFromJsonAsync<List<MovimentoDto>>(U($"/api/estoque/kardex?skuId={azulM}"), Ct))![0].ShouldBe(new MovimentoDto("Venda", -3, "Venda 1"));

        await estoque.EstornarVendaAsync(saida.OperacaoId, "Cancelamento da venda 1", Ct);
        await estoque.EstornarVendaAsync(saida.OperacaoId, "Cancelamento da venda 1", Ct);
        (await Saldo(client, camiseta.Id, azulM)).ShouldBe(2);
        await Should.ThrowAsync<SaidaDeEstoqueRecusadaException>(() =>
            estoque.RegistrarVendaAsync(new SaidaDeEstoque(Guid.NewGuid(), loja.Id, "X", [new ItemDeSaida(azulM, 1)], true), Ct)); // loja de outra empresa
    }

    [Fact]
    public async Task Financeiro_VendaEmDinheiroEntraNaHora_CartaoFicaAReceber_CancelarEstornaTudo()
    {
        var client = await Entrar(_api);
        var empresa = await EmpresaCentro();
        var caixa = (await (await client.PostAsJsonAsync(U("/api/financeiro/contas"), new { nome = "Caixa", tipo = "Caixa", saldoInicial = 100m, saldoInicialEm = "2026-10-01" }, Ct))
            .Content.ReadFromJsonAsync<ContaDto>(Ct))!;
        var hoje = new DateOnly(2026, 10, 17);

        await using (var escopo = Escopo())
        {
            var receber = escopo.ServiceProvider.GetRequiredService<IContasAReceber>();
            (await receber.ContaValidaAsync(empresa, caixa.Id, Ct)).ShouldBeTrue();
            (await receber.ContaValidaAsync(Guid.NewGuid(), caixa.Id, Ct)).ShouldBeFalse();
            var venda = new ContasAReceberDeVenda(empresa, "Venda", "v-1", null, "Maria", hoje,
            [
                new RecebimentoDeVenda("Venda 1 — Dinheiro", [new ParcelaAReceber(hoje, 50m)], caixa.Id, JaRecebido: true, null),
                new RecebimentoDeVenda("Venda 1 — Crédito 2x", [new ParcelaAReceber(hoje.AddDays(30), 48m), new ParcelaAReceber(hoje.AddDays(60), 48m)], null, false, "Bruto R$ 100,00, taxa 4%"),
            ]);
            await receber.RegistrarVendaAsync(venda, Ct);
            await receber.RegistrarVendaAsync(venda, Ct); // não duplica
        }

        (await client.GetFromJsonAsync<List<ContaDto>>(U("/api/financeiro/contas"), Ct))!.Single().Saldo.ShouldBe(150m);
        var abertas = (await client.GetFromJsonAsync<ListaDto>(U("/api/financeiro/titulos?tipo=receber&situacao=todas"), Ct))!.Itens;
        abertas.Select(i => (i.Descricao, i.Valor, i.Situacao)).ShouldBe(
            [("Venda 1 — Dinheiro", 50m, "Paga"), ("Venda 1 — Crédito 2x", 48m, "Aberta"), ("Venda 1 — Crédito 2x", 48m, "Aberta")]);

        await using (var escopo = Escopo())
            await escopo.ServiceProvider.GetRequiredService<IContasAReceber>().CancelarOrigemAsync("Venda", "v-1", "Ana", Ct);

        (await client.GetFromJsonAsync<List<ContaDto>>(U("/api/financeiro/contas"), Ct))!.Single().Saldo.ShouldBe(100m);
        (await client.GetFromJsonAsync<ListaDto>(U("/api/financeiro/titulos?tipo=receber&situacao=canceladas"), Ct))!.Itens.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Cadastros_VendedorComComissao()
    {
        var client = await Entrar(_api);
        var resposta = await client.PostAsJsonAsync(U("/api/vendedores"), new { nome = "Bia", comissaoPercentual = 3.5m }, Ct);
        var id = (await resposta.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct)).GetProperty("id").GetGuid();

        await using var escopo = Escopo();
        var vendedores = escopo.ServiceProvider.GetRequiredService<IDiretorioDeVendedores>();

        (await vendedores.ObterAsync(id, Ct))!.ShouldBe(new VendedorResumo(id, "Bia", 3.5m, true, null));
        (await vendedores.ObterAsync(Guid.NewGuid(), Ct)).ShouldBeNull();
    }
}
