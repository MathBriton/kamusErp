using System.Net;
using System.Net.Http.Json;
using Kamus.Modules.Indicadores.Aplicacao;
using Kamus.SharedKernel.Tempo;
using Microsoft.Extensions.DependencyInjection;
using static Kamus.IntegrationTests.Estoque.ApiDoEstoque;

namespace Kamus.IntegrationTests.Indicadores;

/// <summary>Dashboard (/api/indicadores) com vendas, financeiro, estoque, condicionais e alertas; resumo diário no sininho.</summary>
public sealed class IndicadoresTests : IAsyncDisposable
{
    private readonly Infra.KamusApiFactory _api = NovaApi();
    private static readonly DateOnly Hoje = CalendarioBrasilia.Hoje(TimeProvider.System);

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    private sealed record FormaDto(Guid Id, string Nome, string Tipo, Guid? ContaId, decimal TaxaAVista, decimal TaxaParcelado, int MaxParcelas, int PrazoDias);
    private sealed record PessoaDto(Guid Id, string Nome);
    private sealed record ContaDto(Guid Id, decimal Saldo);

    private sealed record CartaoDto(decimal Total, int Vendas, int Pecas, decimal TicketMedio, decimal TotalAnterior, decimal? Variacao);
    private sealed record DiaDto(DateOnly Data, int Vendas, int Pecas, decimal Total);
    private sealed record NomeDto(string Nome, int Quantidade, decimal Total);
    private sealed record MaisVendidoDto(Guid SkuId, string Descricao, int Pecas, decimal Total);
    private sealed record VendasDto(CartaoDto Periodo, CartaoDto Mes, List<DiaDto> Ultimos30Dias, List<NomeDto> PorForma, List<NomeDto> PorVendedor,
        List<MaisVendidoDto> MaisVendidos, bool SomenteAsMinhas);
    private sealed record CompromissosDto(decimal Hoje, decimal Amanha, decimal ProximosSeteDias, decimal Vencido, int ParcelasVencidas);
    private sealed record SaldoDto(Guid Id, string Nome, string Tipo, decimal Saldo);
    private sealed record FinanceiroDto(List<SaldoDto> Contas, decimal SaldoTotal, CompromissosDto AReceber, CompromissosDto APagar, decimal CrediarioEmAtraso,
        int ClientesEmAtraso);
    private sealed record QuebraDto(Guid ProdutoId, string Descricao, List<string> Esgotados);
    private sealed record EstoqueDto(int Pecas, decimal ValorDeCusto, decimal ValorDeVenda, int SkusNegativos, List<QuebraDto> Quebras, int TotalDeQuebras);
    private sealed record CondicionaisDto(int Abertas, int Atrasadas, int Pecas, decimal Valor);
    private sealed record AlertaDto(string Tipo, string Gravidade, string Titulo, string Detalhe, string Link);
    private sealed record DashboardDto(string Periodo, DateOnly De, DateOnly Ate, VendasDto? Vendas, FinanceiroDto? Financeiro, EstoqueDto? Estoque,
        CondicionaisDto? Condicionais, List<AlertaDto> Alertas);

    private sealed record PreferenciasDto(bool Ativo, int Hora, List<int> Dias, bool Vendas, bool Financeiro, bool Estoque, bool Condicionais, bool Crediario,
        bool EnviarSemMovimento, DateOnly? UltimoEnvio);
    private sealed record PreviaDto(string Titulo, List<string> Linhas, bool TemMovimento);
    private sealed record NotificacaoDto(Guid Id, string Tipo, string Titulo, string Mensagem);
    private sealed record NotificacoesDto(List<NotificacaoDto> Itens);

    private sealed record Cenario(HttpClient Client, ProdutoDto Camiseta, Guid LojaId, FormaDto Pix, PessoaDto Maria);

    /// <summary>
    /// Estoque: 5 Azul M, 5 Azul G e 1 Azul P (custo 20). Venda de ontem: 1 G. Vendas de hoje: 2 M e 1 P (o P esgota: quebra de grade).
    /// Condicional atrasada com 1 G. Caixa com R$ 100 de saldo inicial; PIX entra no caixa. Conta a pagar vencida (R$ 50) e outra vencendo hoje (R$ 30).
    /// </summary>
    private async Task<Cenario> Preparar()
    {
        var client = await Entrar(_api);
        var camiseta = await CriarCamiseta(client);
        var loja = (await Lojas(client)).Single();
        await Entrada(client, loja.Id,
            new { skuId = Sku(camiseta, "Azul", "M").Id, quantidade = 5, custoUnitario = 20m },
            new { skuId = Sku(camiseta, "Azul", "G").Id, quantidade = 5, custoUnitario = 20m },
            new { skuId = Sku(camiseta, "Azul", "P").Id, quantidade = 1, custoUnitario = 20m });
        var caixa = (await (await client.PostAsJsonAsync(U("/api/financeiro/contas"), new { nome = "Caixa", tipo = "Caixa", saldoInicial = 100m, saldoInicialEm = Hoje.AddDays(-30) }, Ct))
            .Content.ReadFromJsonAsync<ContaDto>(Ct))!;
        (await client.PostAsync(U("/api/vendas/formas/padrao"), null, Ct)).EnsureSuccessStatusCode();
        var pix = (await client.GetFromJsonAsync<List<FormaDto>>(U("/api/vendas/formas"), Ct))!.Single(f => f.Tipo == "Pix");
        (await client.PutAsJsonAsync(U($"/api/vendas/formas/{pix.Id}"), new { pix.Nome, pix.Tipo, contaId = caixa.Id, pix.TaxaAVista, pix.TaxaParcelado, pix.MaxParcelas, pix.PrazoDias }, Ct))
            .EnsureSuccessStatusCode();
        var maria = (await (await client.PostAsJsonAsync(U("/api/pessoas"), new { tipo = "Fisica", nome = "Maria Souza", ehCliente = true, ehFornecedor = false, aceitaMarketing = false }, Ct))
            .Content.ReadFromJsonAsync<PessoaDto>(Ct))!;
        var c = new Cenario(client, camiseta, loja.Id, pix, maria);

        await Vender(client, c, Hoje.AddDays(-1), ("G", 1));
        await Vender(client, c, Hoje, ("M", 2));
        await Vender(client, c, Hoje, ("P", 1));
        var condicional = await client.PostAsJsonAsync(U("/api/vendas/condicionais"), new
        {
            lojaId = loja.Id,
            clienteId = maria.Id,
            clienteNome = maria.Nome,
            data = Hoje.AddDays(-5),
            prazo = Hoje.AddDays(-3),
            itens = new[] { new { skuId = Sku(camiseta, "Azul", "G").Id, quantidade = 1 } },
        }, Ct);
        condicional.StatusCode.ShouldBe(HttpStatusCode.Created, await condicional.Content.ReadAsStringAsync(Ct));
        await ContaAPagar(client, "Aluguel atrasado", Hoje.AddDays(-1), 50m);
        await ContaAPagar(client, "Energia", Hoje, 30m);
        return c;
    }

    private static async Task Vender(HttpClient client, Cenario c, DateOnly data, params (string Tamanho, int Quantidade)[] itens)
    {
        var total = itens.Sum(i => i.Quantidade) * 59.90m;
        var r = await client.PostAsJsonAsync(U("/api/vendas"), new
        {
            lojaId = c.LojaId,
            data,
            itens = itens.Select(i => new { skuId = Sku(c.Camiseta, "Azul", i.Tamanho).Id, quantidade = i.Quantidade, desconto = 0m }),
            descontoGeral = 0m,
            pagamentos = new[] { new { formaId = c.Pix.Id, valor = total, parcelas = 1 } },
        }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync(Ct));
    }

    private static async Task ContaAPagar(HttpClient client, string descricao, DateOnly vencimento, decimal valor)
    {
        var r = await client.PostAsJsonAsync(U("/api/financeiro/titulos"), new { tipo = "Pagar", descricao, parcelas = new[] { new { vencimento, valor } } }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Dashboard_DoDono_TrazVendasFinanceiroEstoqueCondicionaisEAlertas()
    {
        var c = await Preparar();

        var d = (await c.Client.GetFromJsonAsync<DashboardDto>(U("/api/indicadores?periodo=hoje"), Ct))!;

        d.Periodo.ShouldBe("hoje");
        (d.De, d.Ate).ShouldBe((Hoje, Hoje));
        var v = d.Vendas.ShouldNotBeNull();
        v.SomenteAsMinhas.ShouldBeFalse();
        v.Periodo.ShouldBe(new CartaoDto(179.70m, 2, 3, 89.85m, 59.90m, 200m));
        v.Mes.Total.ShouldBe(Hoje.Day == 1 ? 179.70m : 239.60m);
        v.Ultimos30Dias.Count.ShouldBe(30);
        v.Ultimos30Dias[^1].ShouldBe(new DiaDto(Hoje, 2, 3, 179.70m));
        v.Ultimos30Dias[^2].Total.ShouldBe(59.90m);
        v.PorForma.ShouldBe([new NomeDto("PIX", 2, 179.70m)]);
        v.MaisVendidos[0].Pecas.ShouldBe(2);
        v.MaisVendidos[0].Descricao.ShouldContain("Azul M");

        var f = d.Financeiro.ShouldNotBeNull();
        f.SaldoTotal.ShouldBe(100m + 59.90m + 179.70m);
        f.Contas.Single().Nome.ShouldBe("Caixa");
        f.APagar.ShouldBe(new CompromissosDto(30m, 0m, 30m, 50m, 1));
        f.AReceber.Vencido.ShouldBe(0m);

        var e = d.Estoque.ShouldNotBeNull();
        e.Pecas.ShouldBe(3 + 3);
        e.ValorDeCusto.ShouldBe(120m);
        e.ValorDeVenda.ShouldBe(6 * 59.90m);
        e.Quebras.Single().Esgotados.ShouldBe(["Azul P"]);

        d.Condicionais.ShouldBe(new CondicionaisDto(1, 1, 1, 59.90m));
        d.Alertas.Select(a => a.Tipo).ShouldBe(["contas_a_pagar_vencidas", "condicionais_atrasadas", "quebra_de_grade"], ignoreOrder: true);
        d.Alertas.Single(a => a.Tipo == "contas_a_pagar_vencidas").Link.ShouldBe("/financeiro/contas-a-pagar");
    }

    [Fact]
    public async Task Dashboard_PeriodoDoMes_ComparaComOMesmoTrechoDoMesAnterior()
    {
        var c = await Preparar();

        var d = (await c.Client.GetFromJsonAsync<DashboardDto>(U("/api/indicadores?periodo=mes"), Ct))!;

        (d.De, d.Ate).ShouldBe((new DateOnly(Hoje.Year, Hoje.Month, 1), Hoje));
        d.Vendas!.Periodo.ShouldBe(d.Vendas.Mes);
        (await c.Client.GetAsync(U("/api/indicadores?periodo=ano"), Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Dashboard_DoVendedor_MostraSoAsPropriasVendasSemFinanceiroNemEstoque()
    {
        var c = await Preparar();
        var vendedora = await Entrar(_api, "vendedor@kamus.local");
        await Vender(vendedora, c, Hoje, ("M", 1));

        var d = (await vendedora.GetFromJsonAsync<DashboardDto>(U("/api/indicadores?periodo=hoje"), Ct))!;

        d.Vendas!.SomenteAsMinhas.ShouldBeTrue();
        d.Vendas.Periodo.Total.ShouldBe(59.90m);
        d.Financeiro.ShouldBeNull();
        d.Estoque.ShouldBeNull();
        d.Alertas.ShouldAllBe(a => a.Tipo == "condicionais_atrasadas");
    }

    [Fact]
    public async Task ResumoDiario_PreferenciasPreviaEEnvioNoSininho()
    {
        var c = await Preparar();

        var padrao = (await c.Client.GetFromJsonAsync<PreferenciasDto>(U("/api/indicadores/resumo-diario"), Ct))!;
        padrao.ShouldBeEquivalentTo(new PreferenciasDto(true, 20, [0, 1, 2, 3, 4, 5, 6], true, true, true, true, true, false, null));

        var r = await c.Client.PutAsJsonAsync(U("/api/indicadores/resumo-diario"), padrao with { Hora = 8, Dias = [1, 2, 3, 4, 5], Estoque = false }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.OK, await r.Content.ReadAsStringAsync(Ct));
        (await c.Client.GetFromJsonAsync<PreferenciasDto>(U("/api/indicadores/resumo-diario"), Ct))!.Hora.ShouldBe(8);
        (await c.Client.PutAsJsonAsync(U("/api/indicadores/resumo-diario"), padrao with { Hora = 24 }, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var previa = (await c.Client.GetFromJsonAsync<PreviaDto>(U("/api/indicadores/resumo-diario/previa"), Ct))!;
        previa.TemMovimento.ShouldBeTrue();
        previa.Titulo.ShouldBe($"Resumo do dia {Hoje:dd/MM}");
        previa.Linhas.ShouldContain(l => l.StartsWith("Vendas: R$ 179,70 em 2 vendas", StringComparison.Ordinal));
        previa.Linhas.ShouldContain(l => l.Contains("1 condicional atrasada", StringComparison.Ordinal));
        previa.Linhas.ShouldNotContain(l => l.Contains("Quebra de grade", StringComparison.Ordinal));

        (await c.Client.PostAsync(U("/api/indicadores/resumo-diario/enviar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var notificacoes = (await c.Client.GetFromJsonAsync<NotificacoesDto>(U("/api/notificacoes"), Ct))!.Itens;
        var resumo = notificacoes.Single(n => n.Tipo == "resumo_diario");
        resumo.Titulo.ShouldBe(previa.Titulo);
        resumo.Mensagem.ShouldContain("Vendas: R$ 179,70");
    }

    [Fact]
    public async Task RotinaDoResumo_EnviaUmaVezNoHorarioDoUsuario()
    {
        var c = await Preparar();
        var agora = DateTimeOffset.UtcNow;
        var horaDeBrasilia = agora.ToOffset(TimeSpan.FromHours(-3)).Hour;
        var padrao = (await c.Client.GetFromJsonAsync<PreferenciasDto>(U("/api/indicadores/resumo-diario"), Ct))!;
        (await c.Client.PutAsJsonAsync(U("/api/indicadores/resumo-diario"), padrao with { Hora = horaDeBrasilia }, Ct)).EnsureSuccessStatusCode();

        if (horaDeBrasilia > 0)
        {
            await Rodar(agora.AddHours(-1));
            (await Resumos(c.Client)).ShouldBe(0, "antes do horário não envia");
        }
        await Rodar(agora);
        await Rodar(agora.AddMinutes(5));
        (await Resumos(c.Client)).ShouldBe(1);
        (await c.Client.GetFromJsonAsync<PreferenciasDto>(U("/api/indicadores/resumo-diario"), Ct))!.UltimoEnvio.ShouldBe(Hoje);
    }

    private async Task Rodar(DateTimeOffset agora)
    {
        await using var escopo = _api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<GeradorDeResumos>().ExecutarAsync(agora, Ct);
    }

    private static async Task<int> Resumos(HttpClient client) =>
        (await client.GetFromJsonAsync<NotificacoesDto>(U("/api/notificacoes"), Ct))!.Itens.Count(n => n.Tipo == "resumo_diario");
}
