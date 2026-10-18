using System.Net;
using System.Net.Http.Json;
using Kamus.SharedKernel.Tempo;
using static Kamus.IntegrationTests.Estoque.ApiDoEstoque;

namespace Kamus.IntegrationTests.Vendas;

/// <summary>Troca vinculada à venda (estoque, diferença paga ou vale-troca), vale como forma de pagamento e fechamento de comissões.</summary>
public sealed class TrocasTests : IAsyncDisposable
{
    private readonly Infra.KamusApiFactory _api = NovaApi();
    private static readonly DateOnly Hoje = CalendarioBrasilia.Hoje(TimeProvider.System);

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    private sealed record FormaDto(Guid Id, string Nome, string Tipo, Guid? ContaId, decimal TaxaAVista, decimal TaxaParcelado, int MaxParcelas, int PrazoDias);
    private sealed record VendaDto(Guid Id, int Numero, decimal Total, string Status, List<PagamentoDto> Pagamentos);
    private sealed record PagamentoDto(string FormaNome, string Tipo, decimal Valor);
    private sealed record DevolvivelDto(Guid SkuId, string Descricao, int Vendidas, int JaDevolvidas, int Disponiveis, decimal ValorPorPeca);
    private sealed record DevolviveisDto(Guid VendaId, int Numero, DateOnly Data, int DiasDesdeAVenda, int PrazoDeTrocaDias, bool DentroDoPrazo, List<DevolvivelDto> Itens);
    private sealed record ValeDto(Guid Id, string Codigo, string? ClienteNome, decimal Valor, decimal Saldo, DateOnly ValidoAte, string Situacao);
    private sealed record TrocaDto(Guid Id, int Numero, int VendaNumero, string Motivo, decimal ValorDevolvido, decimal ValorLevado, decimal Diferenca, decimal Troco,
        decimal Comissao, ValeDto? Vale, List<string> Avisos);
    private sealed record ListaDeValesDto(List<ValeDto> Itens, decimal SaldoAtivo, decimal SaldoVencido);
    private sealed record ComissaoDto(Guid? VendedorId, string Vendedor, int Vendas, decimal Total, decimal Comissao, decimal ComissaoPendente);
    private sealed record FechamentoDto(Guid Id, string VendedorNome, int Vendas, int Trocas, decimal TotalVendido, decimal Comissao, bool ContaAPagarGerada);
    private sealed record ContaDto(Guid Id, decimal Saldo);
    private sealed record CelulaDto(Guid SkuId, int Quantidade);
    private sealed record MatrizDto(List<CelulaDto> Celulas);
    private sealed record LinhaFinDto(string Descricao, decimal Valor, string Situacao);
    private sealed record ListaFinDto(List<LinhaFinDto> Itens);
    private sealed record VendedorDto(Guid Id, string Nome);

    private sealed record Cenario(HttpClient Client, ProdutoDto Camiseta, Guid LojaId, Guid AzulM, Guid AzulG, Guid PretoP, ContaDto Caixa, Dictionary<string, FormaDto> Formas,
        VendedorDto Bia);

    private async Task<Cenario> Preparar()
    {
        var client = await Entrar(_api);
        var camiseta = await CriarCamiseta(client);
        var loja = (await Lojas(client)).Single();
        var (azulM, azulG, pretoP) = (Sku(camiseta, "Azul", "M").Id, Sku(camiseta, "Azul", "G").Id, Sku(camiseta, "Preto", "P").Id);
        await Entrada(client, loja.Id, new { skuId = azulM, quantidade = 5, custoUnitario = 20m }, new { skuId = azulG, quantidade = 5, custoUnitario = 20m });
        var caixa = (await (await client.PostAsJsonAsync(U("/api/financeiro/contas"), new { nome = "Caixa", tipo = "Caixa", saldoInicial = 0m, saldoInicialEm = Hoje.AddDays(-60) }, Ct))
            .Content.ReadFromJsonAsync<ContaDto>(Ct))!;
        (await client.PostAsync(U("/api/vendas/formas/padrao"), null, Ct)).EnsureSuccessStatusCode();
        var formas = (await client.GetFromJsonAsync<List<FormaDto>>(U("/api/vendas/formas"), Ct))!.ToDictionary(f => f.Nome);
        foreach (var nome in new[] { "Dinheiro", "PIX" })
        {
            var f = formas[nome];
            (await client.PutAsJsonAsync(U($"/api/vendas/formas/{f.Id}"), new { f.Nome, f.Tipo, contaId = caixa.Id, f.TaxaAVista, f.TaxaParcelado, f.MaxParcelas, f.PrazoDias }, Ct))
                .EnsureSuccessStatusCode();
        }
        var bia = (await (await client.PostAsJsonAsync(U("/api/vendedores"), new { nome = "Bia", comissaoPercentual = 5m }, Ct)).Content.ReadFromJsonAsync<VendedorDto>(Ct))!;
        return new Cenario(client, camiseta, loja.Id, azulM, azulG, pretoP, caixa, formas, bia);
    }

    private static async Task<VendaDto> Vender(Cenario c, object[] itens, object[] pagamentos, DateOnly? data = null, decimal descontoGeral = 0)
    {
        var r = await c.Client.PostAsJsonAsync(U("/api/vendas"), new { lojaId = c.LojaId, data, clienteNome = "Maria", vendedorId = c.Bia.Id, itens, descontoGeral, pagamentos }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync(Ct));
        return (await r.Content.ReadFromJsonAsync<VendaDto>(Ct))!;
    }

    private static Task<HttpResponseMessage> Trocar(HttpClient client, Guid vendaId, object[] devolvidos, object[]? levados = null, object[]? pagamentos = null,
        bool foraDoPrazo = false, string motivo = "Tamanho") =>
        client.PostAsJsonAsync(U("/api/vendas/trocas"), new { vendaId, motivo, devolvidos, levados = levados ?? [], pagamentos = pagamentos ?? [], foraDoPrazo }, Ct);

    private static async Task<int> Saldo(Cenario c, Guid skuId) =>
        (await c.Client.GetFromJsonAsync<MatrizDto>(U($"/api/estoque/posicao/{c.Camiseta.Id}"), Ct))!.Celulas.Single(x => x.SkuId == skuId).Quantidade;

    [Fact]
    public async Task Troca_PorTamanhoSemDiferenca_DevolveEstoqueESaiAPecaNova()
    {
        var c = await Preparar();
        var venda = await Vender(c, [new { skuId = c.AzulM, quantidade = 2, desconto = 0m }], [new { formaId = c.Formas["PIX"].Id, valor = 119.80m, parcelas = 1 }]);

        var devolviveis = (await c.Client.GetFromJsonAsync<DevolviveisDto>(U($"/api/vendas/{venda.Id}/devolviveis"), Ct))!;
        (devolviveis.DentroDoPrazo, devolviveis.PrazoDeTrocaDias).ShouldBe((true, 30));
        devolviveis.Itens.Single().ShouldBe(new DevolvivelDto(c.AzulM, devolviveis.Itens[0].Descricao, 2, 0, 2, 59.90m));

        var r = await Trocar(c.Client, venda.Id, [new { skuId = c.AzulM, quantidade = 1, destino = "Estoque" }], [new { skuId = c.AzulG, quantidade = 1, desconto = 0m }]);
        r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync(Ct));
        var troca = (await r.Content.ReadFromJsonAsync<TrocaDto>(Ct))!;
        (troca.Numero, troca.VendaNumero, troca.ValorDevolvido, troca.ValorLevado, troca.Diferenca, troca.Vale).ShouldBe((1, 1, 59.90m, 59.90m, 0m, (ValeDto?)null));

        ((await Saldo(c, c.AzulM)), (await Saldo(c, c.AzulG))).ShouldBe((4, 4));
        (await c.Client.GetFromJsonAsync<DevolviveisDto>(U($"/api/vendas/{venda.Id}/devolviveis"), Ct))!.Itens.Single().Disponiveis.ShouldBe(1);
        var outra = await Trocar(c.Client, venda.Id, [new { skuId = c.AzulM, quantidade = 2, destino = "Estoque" }]);
        outra.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await outra.Content.ReadAsStringAsync(Ct)).ShouldContain("resta 1");
    }

    [Fact]
    public async Task Troca_SemLevar_GeraVale_QuePagaOutraVendaEmParte_CancelarDevolveOSaldo()
    {
        var c = await Preparar();
        var venda = await Vender(c, [new { skuId = c.AzulM, quantidade = 2, desconto = 0m }], [new { formaId = c.Formas["PIX"].Id, valor = 109.80m, parcelas = 1 }], descontoGeral: 10m);

        var r = await Trocar(c.Client, venda.Id, [new { skuId = c.AzulM, quantidade = 1, destino = "Avariado" }], motivo: "Defeito");
        r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync(Ct));
        var troca = (await r.Content.ReadFromJsonAsync<TrocaDto>(Ct))!;
        troca.ValorDevolvido.ShouldBe(54.90m);
        troca.Comissao.ShouldBe(-2.75m);
        var vale = troca.Vale!;
        (vale.Valor, vale.Saldo, vale.ValidoAte, vale.Situacao, vale.ClienteNome).ShouldBe((54.90m, 54.90m, Hoje.AddDays(90), "Ativo", "Maria"));
        (await Saldo(c, c.AzulM)).ShouldBe(3); // avariada não volta

        var porCodigo = (await c.Client.GetFromJsonAsync<ValeDto>(U($"/api/vendas/vales/por-codigo/{vale.Codigo.ToLowerInvariant()}"), Ct))!;
        porCodigo.Id.ShouldBe(vale.Id);

        var excede = await c.Client.PostAsJsonAsync(U("/api/vendas"), new
        {
            lojaId = c.LojaId,
            itens = new[] { new { skuId = c.AzulG, quantidade = 2, desconto = 0m } },
            descontoGeral = 0m,
            pagamentos = new[] { new { formaId = c.Formas["Vale-troca"].Id, valor = 60m, parcelas = 1, valeCodigo = vale.Codigo } },
        }, Ct);
        excede.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await excede.Content.ReadAsStringAsync(Ct)).ShouldContain("R$ 54,90");

        var comVale = await Vender(c, [new { skuId = c.AzulG, quantidade = 2, desconto = 0m }],
            [new { formaId = c.Formas["Vale-troca"].Id, valor = 50m, parcelas = 1, valeCodigo = vale.Codigo }, new { formaId = c.Formas["Dinheiro"].Id, valor = 69.80m, parcelas = 1 }]);
        comVale.Pagamentos.Select(p => (p.Tipo, p.Valor)).ShouldBe([("ValeTroca", 50m), ("Dinheiro", 69.80m)]);
        (await c.Client.GetFromJsonAsync<ValeDto>(U($"/api/vendas/vales/por-codigo/{vale.Codigo}"), Ct))!.Saldo.ShouldBe(4.90m);
        (await c.Client.GetFromJsonAsync<List<ContaDto>>(U("/api/financeiro/contas"), Ct))!.Single().Saldo.ShouldBe(109.80m + 69.80m);

        (await c.Client.PostAsJsonAsync(U($"/api/vendas/{comVale.Id}/cancelar"), new { motivo = "Desistiu" }, Ct)).EnsureSuccessStatusCode();
        (await c.Client.GetFromJsonAsync<ValeDto>(U($"/api/vendas/vales/por-codigo/{vale.Codigo}"), Ct))!.Saldo.ShouldBe(54.90m);

        var lista = (await c.Client.GetFromJsonAsync<ListaDeValesDto>(U("/api/vendas/vales?situacao=ativos"), Ct))!;
        (lista.Itens.Single().Codigo, lista.SaldoAtivo).ShouldBe((vale.Codigo, 54.90m));
        (await c.Client.GetStringAsync(U("/api/relatorios/vales-troca.csv?situacao=todos"), Ct)).ShouldContain(vale.Codigo);

        var vendedora = await Entrar(_api, "vendedor@kamus.local");
        (await vendedora.PostAsJsonAsync(U($"/api/vendas/vales/{vale.Id}/cancelar"), new { motivo = "Engano" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await c.Client.PostAsJsonAsync(U($"/api/vendas/vales/{vale.Id}/cancelar"), new { motivo = "Engano" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await c.Client.GetFromJsonAsync<ValeDto>(U($"/api/vendas/vales/por-codigo/{vale.Codigo}"), Ct))!.Situacao.ShouldBe("Cancelado");
    }

    [Fact]
    public async Task Troca_PecaMaisCara_ClientePagaDiferenca_ForaDoPrazoSoGestao()
    {
        var c = await Preparar();
        var antiga = await Vender(c, [new { skuId = c.AzulM, quantidade = 1, desconto = 0m }], [new { formaId = c.Formas["PIX"].Id, valor = 59.90m, parcelas = 1 }], data: Hoje.AddDays(-40));

        var vendedora = await Entrar(_api, "vendedor@kamus.local");
        var pelaVendedora = (await vendedora.GetFromJsonAsync<DevolviveisDto>(U($"/api/vendas/devolviveis?numero={antiga.Numero}"), Ct))!; // venda de outra pessoa
        (pelaVendedora.VendaId, pelaVendedora.DentroDoPrazo, pelaVendedora.DiasDesdeAVenda).ShouldBe((antiga.Id, false, 40));
        (await vendedora.GetAsync(U("/api/vendas/devolviveis?numero=999"), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var negada = await Trocar(vendedora, antiga.Id, [new { skuId = c.AzulM, quantidade = 1, destino = "Estoque" }], foraDoPrazo: true);
        negada.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await negada.Content.ReadAsStringAsync(Ct)).ShouldContain("40 dias");
        (await Trocar(c.Client, antiga.Id, [new { skuId = c.AzulM, quantidade = 1, destino = "Estoque" }])).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var semPagar = await Trocar(c.Client, antiga.Id, [new { skuId = c.AzulM, quantidade = 1, destino = "Estoque" }],
            [new { skuId = c.AzulG, quantidade = 2, desconto = 0m }], foraDoPrazo: true);
        (await semPagar.Content.ReadAsStringAsync(Ct)).ShouldContain("faltam R$ 59,90");

        var r = await Trocar(c.Client, antiga.Id, [new { skuId = c.AzulM, quantidade = 1, destino = "Estoque" }], [new { skuId = c.AzulG, quantidade = 2, desconto = 0m }],
            [new { formaId = c.Formas["Dinheiro"].Id, valor = 60m, parcelas = 1 }], foraDoPrazo: true);
        r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync(Ct));
        var troca = (await r.Content.ReadFromJsonAsync<TrocaDto>(Ct))!;
        (troca.Diferenca, troca.Troco, troca.Comissao).ShouldBe((59.90m, 0.10m, 3.00m));
        (await c.Client.GetFromJsonAsync<List<ContaDto>>(U("/api/financeiro/contas"), Ct))!.Single().Saldo.ShouldBe(59.90m + 59.90m);
        (await c.Client.GetFromJsonAsync<ListaFinDto>(U("/api/financeiro/titulos?tipo=receber&situacao=todas"), Ct))!.Itens
            .ShouldContain(l => l.Descricao.StartsWith("Troca nº 1", StringComparison.Ordinal) && l.Valor == 59.90m);
    }

    [Fact]
    public async Task Fechamento_SomaVendasETrocas_GeraContaAPagar_BloqueiaCancelar_Reabre()
    {
        var c = await Preparar();
        var v1 = await Vender(c, [new { skuId = c.AzulM, quantidade = 2, desconto = 0m }], [new { formaId = c.Formas["PIX"].Id, valor = 119.80m, parcelas = 1 }]);
        var v2 = await Vender(c, [new { skuId = c.AzulG, quantidade = 1, desconto = 0m }], [new { formaId = c.Formas["PIX"].Id, valor = 59.90m, parcelas = 1 }]);
        (await Trocar(c.Client, v1.Id, [new { skuId = c.AzulM, quantidade = 1, destino = "Estoque" }])).EnsureSuccessStatusCode(); // -59,90 → -3,00 de comissão

        var periodo = $"de={Hoje.AddDays(-1):yyyy-MM-dd}&ate={Hoje:yyyy-MM-dd}";
        var comissoes = (await c.Client.GetFromJsonAsync<List<ComissaoDto>>(U($"/api/vendas/comissoes?{periodo}"), Ct))!;
        comissoes.Single().ShouldBe(new ComissaoDto(c.Bia.Id, "Bia", 2, 119.80m, 5.99m, 5.99m));

        var vendedora = await Entrar(_api, "vendedor@kamus.local");
        var corpo = new { vendedorId = c.Bia.Id, de = Hoje.AddDays(-1), ate = Hoje, gerarContaAPagar = true, vencimento = Hoje.AddDays(5) };
        (await vendedora.PostAsJsonAsync(U("/api/vendas/comissoes/fechamentos"), corpo, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var r = await c.Client.PostAsJsonAsync(U("/api/vendas/comissoes/fechamentos"), corpo, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync(Ct));
        var fechamento = (await r.Content.ReadFromJsonAsync<FechamentoDto>(Ct))!;
        fechamento.ShouldBe(new FechamentoDto(fechamento.Id, "Bia", 2, 1, 119.80m, 5.99m, true));

        (await c.Client.PostAsJsonAsync(U("/api/vendas/comissoes/fechamentos"), corpo, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest); // nada a fechar
        (await c.Client.GetFromJsonAsync<List<ComissaoDto>>(U($"/api/vendas/comissoes?{periodo}"), Ct))!.Single().ComissaoPendente.ShouldBe(0m);
        var pagar = (await c.Client.GetFromJsonAsync<ListaFinDto>(U("/api/financeiro/titulos?tipo=pagar&situacao=abertas"), Ct))!.Itens;
        pagar.ShouldContain(l => l.Descricao.StartsWith("Comissão de Bia", StringComparison.Ordinal) && l.Valor == 5.99m);

        var cancelar = await c.Client.PostAsJsonAsync(U($"/api/vendas/{v2.Id}/cancelar"), new { motivo = "Teste" }, Ct);
        cancelar.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await cancelar.Content.ReadAsStringAsync(Ct)).ShouldContain("reabra");

        (await c.Client.PostAsync(U($"/api/vendas/comissoes/fechamentos/{fechamento.Id}/reabrir"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await c.Client.GetFromJsonAsync<List<FechamentoDto>>(U("/api/vendas/comissoes/fechamentos"), Ct))!.ShouldBeEmpty();
        (await c.Client.GetFromJsonAsync<ListaFinDto>(U("/api/financeiro/titulos?tipo=pagar&situacao=canceladas"), Ct))!.Itens.ShouldContain(l => l.Valor == 5.99m);
        (await c.Client.PostAsJsonAsync(U($"/api/vendas/{v2.Id}/cancelar"), new { motivo = "Teste" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var comTroca = await c.Client.PostAsJsonAsync(U($"/api/vendas/{v1.Id}/cancelar"), new { motivo = "Teste" }, Ct);
        comTroca.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await comTroca.Content.ReadAsStringAsync(Ct)).ShouldContain("troca");
    }
}
