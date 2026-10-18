using System.Net;
using System.Net.Http.Json;
using Kamus.SharedKernel.Tempo;
using static Kamus.IntegrationTests.Estoque.ApiDoEstoque;

namespace Kamus.IntegrationTests.Financeiro;

/// <summary>Crediário próprio: limite por cliente, venda a prazo em parcelas mensais, bloqueio por atraso, recebimento com multa/juros, carnê, recibo e carteira.</summary>
public sealed class CrediarioTests : IAsyncDisposable
{
    private readonly Infra.KamusApiFactory _api = NovaApi();
    private static readonly DateOnly Hoje = CalendarioBrasilia.Hoje(TimeProvider.System);

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    private sealed record FormaDto(Guid Id, string Nome, string Tipo, Guid? ContaId, decimal TaxaAVista, decimal TaxaParcelado, int MaxParcelas, int PrazoDias);
    private sealed record PessoaDto(Guid Id, string Nome);
    private sealed record ContaDto(Guid Id, string Nome, decimal Saldo);
    private sealed record ParcelaDto(Guid ParcelaId, string Descricao, int Numero, int Parcelas, DateOnly Vencimento, decimal Valor, decimal Saldo, string Situacao,
        int DiasDeAtraso, decimal Multa, decimal Juros);
    private sealed record ClienteDto(Guid ClienteId, string ClienteNome, decimal Limite, decimal EmAberto, decimal Disponivel, int DiasDeAtraso, bool Bloqueado, string? Motivo,
        List<ParcelaDto> Parcelas);
    private sealed record LinhaDto(Guid ClienteId, string ClienteNome, decimal Limite, decimal EmAberto, decimal Atrasado, int DiasDeAtraso, decimal Disponivel, bool Bloqueado);
    private sealed record CarteiraDto(decimal AVencer, decimal Ate30, decimal Ate60, decimal Ate90, decimal Acima90, decimal Total, List<LinhaDto> Clientes);
    private sealed record RecebimentoDto(Guid BaixaId, decimal Valor, decimal Multa, decimal Juros, decimal Total);
    private sealed record VendaDto(Guid Id, int Numero, decimal Total);
    private sealed record LinhaFinDto(string Descricao, decimal Valor, string Situacao);
    private sealed record ListaFinDto(List<LinhaFinDto> Itens);

    private sealed record Cenario(HttpClient Client, Guid LojaId, Guid AzulM, FormaDto Crediario, FormaDto Pix, PessoaDto Maria, ContaDto Caixa);

    private async Task<Cenario> Preparar()
    {
        var client = await Entrar(_api);
        var camiseta = await CriarCamiseta(client);
        var loja = (await Lojas(client)).Single();
        var azulM = Sku(camiseta, "Azul", "M").Id;
        await Entrada(client, loja.Id, new { skuId = azulM, quantidade = 20, custoUnitario = 20m });
        var caixa = (await (await client.PostAsJsonAsync(U("/api/financeiro/contas"), new { nome = "Caixa", tipo = "Caixa", saldoInicial = 0m, saldoInicialEm = Hoje.AddDays(-90) }, Ct))
            .Content.ReadFromJsonAsync<ContaDto>(Ct))!;
        (await client.PostAsync(U("/api/vendas/formas/padrao"), null, Ct)).EnsureSuccessStatusCode();
        var formas = (await client.GetFromJsonAsync<List<FormaDto>>(U("/api/vendas/formas"), Ct))!;
        var pix = formas.Single(f => f.Tipo == "Pix");
        (await client.PutAsJsonAsync(U($"/api/vendas/formas/{pix.Id}"), new { pix.Nome, pix.Tipo, contaId = caixa.Id, pix.TaxaAVista, pix.TaxaParcelado, pix.MaxParcelas, pix.PrazoDias }, Ct))
            .EnsureSuccessStatusCode();
        var r = await client.PostAsJsonAsync(U("/api/pessoas"), new { tipo = "Fisica", nome = "Maria Souza", ehCliente = true, ehFornecedor = false, aceitaMarketing = false }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync(Ct));
        return new Cenario(client, loja.Id, azulM, formas.Single(f => f.Tipo == "Crediario"), pix, (await r.Content.ReadFromJsonAsync<PessoaDto>(Ct))!, caixa);
    }

    private static Task<HttpResponseMessage> Vender(Cenario c, int quantidade, int parcelas, Guid? clienteId, DateOnly? data = null) =>
        c.Client.PostAsJsonAsync(U("/api/vendas"), new
        {
            lojaId = c.LojaId,
            data,
            clienteId,
            clienteNome = clienteId is null ? null : "Maria Souza",
            itens = new[] { new { skuId = c.AzulM, quantidade, desconto = 0m } },
            descontoGeral = 0m,
            pagamentos = new[] { new { formaId = c.Crediario.Id, valor = 59.90m * quantidade, parcelas } },
        }, Ct);

    private static Task<HttpResponseMessage> DefinirLimite(HttpClient client, Guid clienteId, decimal limite, bool bloqueado = false, string? observacao = null) =>
        client.PutAsJsonAsync(U($"/api/financeiro/crediario/clientes/{clienteId}/limite"), new { clienteNome = "Maria Souza", limite, bloqueado, observacao }, Ct);

    private static async Task<ClienteDto> Cliente(HttpClient client, Guid id) => (await client.GetFromJsonAsync<ClienteDto>(U($"/api/financeiro/crediario/clientes/{id}"), Ct))!;

    private static async Task Recusa(HttpResponseMessage r, string trecho)
    {
        r.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await r.Content.ReadAsStringAsync(Ct)).ShouldContain(trecho);
    }

    [Fact]
    public async Task Venda_NoCrediario_ExigeClienteELimite_GeraParcelasMensais_ConsomeLimite()
    {
        var c = await Preparar();
        c.Crediario.ShouldBe(c.Crediario with { Nome = "Crediário", MaxParcelas = 10, PrazoDias = 30, TaxaAVista = 0m, ContaId = null });

        await Recusa(await Vender(c, 2, 3, null), "cliente cadastrado");
        await Recusa(await Vender(c, 2, 3, c.Maria.Id), "sem limite");

        var vendedora = await Entrar(_api, "vendedor@kamus.local");
        (await DefinirLimite(vendedora, c.Maria.Id, 300m)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await DefinirLimite(c.Client, c.Maria.Id, 300m)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var r = await Vender(c, 2, 3, c.Maria.Id);
        r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync(Ct));
        var maria = await Cliente(vendedora, c.Maria.Id);
        (maria.Limite, maria.EmAberto, maria.Disponivel, maria.Bloqueado).ShouldBe((300m, 119.80m, 180.20m, false));
        maria.Parcelas.Select(p => (p.Numero, p.Vencimento, p.Valor)).ShouldBe(
            [(1, Hoje.AddDays(30), 39.94m), (2, Hoje.AddDays(30).AddMonths(1), 39.93m), (3, Hoje.AddDays(30).AddMonths(2), 39.93m)]);
        maria.Parcelas[0].Descricao.ShouldBe("Venda nº 1 — Crediário 3x");

        await Recusa(await Vender(c, 4, 2, c.Maria.Id), "R$ 180,20");
        await Recusa(await Vender(c, 1, 11, c.Maria.Id), "10 parcelas");

        var venda = (await r.Content.ReadFromJsonAsync<VendaDto>(Ct))!;
        (await c.Client.PostAsJsonAsync(U($"/api/vendas/{venda.Id}/cancelar"), new { motivo = "Desistiu" }, Ct)).EnsureSuccessStatusCode();
        (await Cliente(c.Client, c.Maria.Id)).EmAberto.ShouldBe(0m);
    }

    [Fact]
    public async Task Atraso_BloqueiaNovasCompras_ReceberComEncargos_ReciboCarneECarteira()
    {
        var c = await Preparar();
        (await DefinirLimite(c.Client, c.Maria.Id, 500m)).EnsureSuccessStatusCode();
        (await Vender(c, 2, 2, c.Maria.Id, data: Hoje.AddDays(-60))).StatusCode.ShouldBe(HttpStatusCode.Created);

        var maria = await Cliente(c.Client, c.Maria.Id);
        (maria.DiasDeAtraso, maria.Bloqueado).ShouldBe((30, true));
        maria.Motivo.ShouldBe("Parcela atrasada há 30 dias (bloqueia com 15).");
        var primeira = maria.Parcelas[0];
        (primeira.Vencimento, primeira.Situacao, primeira.DiasDeAtraso, primeira.Multa, primeira.Juros).ShouldBe((Hoje.AddDays(-30), "Vencida", 30, 1.20m, 0.60m));
        await Recusa(await Vender(c, 1, 1, c.Maria.Id), "atrasada há 30 dias");

        var vendedora = await Entrar(_api, "vendedor@kamus.local");
        var contas = (await vendedora.GetFromJsonAsync<List<ContaDto>>(U("/api/financeiro/crediario/contas"), Ct))!;
        var r = await vendedora.PostAsJsonAsync(U($"/api/financeiro/crediario/parcelas/{primeira.ParcelaId}/receber"),
            new { contaId = contas.Single().Id, valor = 59.90m, desconto = 0m }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.OK, await r.Content.ReadAsStringAsync(Ct));
        var recebido = (await r.Content.ReadFromJsonAsync<RecebimentoDto>(Ct))!;
        (recebido.Valor, recebido.Multa, recebido.Juros, recebido.Total).ShouldBe((59.90m, 1.20m, 0.60m, 61.70m));
        (await c.Client.GetFromJsonAsync<List<ContaDto>>(U("/api/financeiro/contas"), Ct))!.Single().Saldo.ShouldBe(61.70m);

        var recibo = await vendedora.GetAsync(U($"/api/financeiro/crediario/baixas/{recebido.BaixaId}/recibo.pdf"), Ct);
        recibo.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        (await recibo.Content.ReadAsByteArrayAsync(Ct))[..4].ShouldBe("%PDF"u8.ToArray());
        var carne = await vendedora.GetAsync(U($"/api/financeiro/crediario/clientes/{c.Maria.Id}/carne.pdf"), Ct);
        (await carne.Content.ReadAsByteArrayAsync(Ct))[..4].ShouldBe("%PDF"u8.ToArray());

        var depois = await Cliente(c.Client, c.Maria.Id);
        (depois.EmAberto, depois.Bloqueado).ShouldBe((59.90m, false));
        depois.Parcelas[0].Situacao.ShouldBe("Paga");

        var carteira = (await c.Client.GetFromJsonAsync<CarteiraDto>(U("/api/financeiro/crediario/carteira"), Ct))!;
        carteira.Total.ShouldBe(59.90m);
        carteira.Clientes.Single().ShouldBe(new LinhaDto(c.Maria.Id, "Maria Souza", 500m, 59.90m, carteira.Clientes[0].Atrasado, depois.DiasDeAtraso, 440.10m, false));
        (await c.Client.GetStringAsync(U("/api/relatorios/inadimplencia.csv"), Ct)).ShouldContain("Maria Souza");

        (await DefinirLimite(c.Client, c.Maria.Id, 500m, bloqueado: true, observacao: "Cheque devolvido")).EnsureSuccessStatusCode();
        await Recusa(await Vender(c, 1, 1, c.Maria.Id), "Cheque devolvido");

        (await c.Client.PutAsJsonAsync(U("/api/financeiro/crediario/configuracao"), new { multaPercentual = 2.5m, jurosMensalPercentual = 1m, diasParaBloquear = 15 }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await c.Client.PutAsJsonAsync(U("/api/financeiro/crediario/configuracao"), new { multaPercentual = 1m, jurosMensalPercentual = 2m, diasParaBloquear = 45 }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await vendedora.PutAsJsonAsync(U("/api/financeiro/crediario/configuracao"), new { multaPercentual = 1m, jurosMensalPercentual = 2m, diasParaBloquear = 45 }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
