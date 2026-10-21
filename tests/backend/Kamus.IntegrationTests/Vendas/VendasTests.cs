using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kamus.SharedKernel.Tempo;
using static Kamus.IntegrationTests.Estoque.ApiDoEstoque;

namespace Kamus.IntegrationTests.Vendas;

/// <summary>Formas de pagamento, registro de venda (estoque, financeiro, comissão), cancelamento, listagem, comissões e relatórios.</summary>
public sealed class VendasTests : IAsyncDisposable
{
    private readonly Infra.KamusApiFactory _api = NovaApi();
    private static readonly DateOnly Hoje = CalendarioBrasilia.Hoje(TimeProvider.System);

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    private sealed record FormaDto(Guid Id, string Nome, string Tipo, Guid? ContaId, decimal TaxaAVista, decimal TaxaParcelado, int MaxParcelas, int PrazoDias, bool Ativa);
    private sealed record ConfiguracaoDto(bool PermitirEstoqueNegativo);
    private sealed record ItemDto(Guid SkuId, string Descricao, int Quantidade, decimal PrecoUnitario, decimal Desconto, decimal Total, decimal? CustoUnitario);
    private sealed record PagamentoDto(string FormaNome, string Tipo, decimal Valor, int Parcelas, decimal TaxaPercentual, decimal ValorTaxa);
    private sealed record VendaDto(Guid Id, int Numero, DateOnly Data, string? ClienteNome, string? VendedorNome, decimal Subtotal, decimal DescontoGeral, decimal Total,
        decimal Troco, int Pecas, decimal Comissao, decimal TaxaTotal, decimal? LucroBruto, string Status, string? NotaExterna, string? MotivoDoCancelamento,
        List<ItemDto> Itens, List<PagamentoDto> Pagamentos, List<string> Avisos);
    private sealed record LinhaDto(Guid Id, int Numero, string? VendedorNome, int Pecas, decimal Total, string Formas, string Status);
    private sealed record ListaDto(List<LinhaDto> Itens, int Quantidade, int Pecas, decimal Total, decimal TicketMedio);
    private sealed record PaginaDeVendasDto(List<LinhaDto> Itens, int Registros, int Pagina, int Tamanho, int Quantidade, int Pecas, decimal Total);
    private sealed record PaginaDto<T>(List<T> Itens, int Total, int Pagina, int Tamanho);
    private sealed record ComissaoDto(Guid? VendedorId, string Vendedor, int Vendas, decimal Total, decimal Comissao);
    private sealed record ContaDto(Guid Id, string Nome, decimal Saldo);
    private sealed record VendedorDto(Guid Id, string Nome);
    private sealed record CelulaDto(Guid SkuId, int Quantidade, decimal Preco);
    private sealed record MatrizDto(List<CelulaDto> Celulas);
    private sealed record LinhaReceberDto(string Descricao, DateOnly Vencimento, decimal Valor, decimal Saldo, string Situacao);
    private sealed record ListaReceberDto(List<LinhaReceberDto> Itens);

    private sealed record Cenario(HttpClient Client, ProdutoDto Camiseta, Guid LojaId, Guid AzulM, Guid PretoG, ContaDto Caixa, Dictionary<string, FormaDto> Formas, VendedorDto Bia);

    private static async Task<Dictionary<string, FormaDto>> Formas(HttpClient client) =>
        (await client.GetFromJsonAsync<List<FormaDto>>(U("/api/vendas/formas"), Ct))!.ToDictionary(f => f.Nome);

    private static async Task<int> Saldo(HttpClient client, Guid produtoId, Guid skuId) =>
        (await client.GetFromJsonAsync<MatrizDto>(U($"/api/estoque/posicao/{produtoId}"), Ct))!.Celulas.Single(c => c.SkuId == skuId).Quantidade;

    private static async Task<decimal> SaldoDaConta(HttpClient client, Guid contaId) =>
        (await client.GetFromJsonAsync<List<ContaDto>>(U("/api/financeiro/contas"), Ct))!.Single(c => c.Id == contaId).Saldo;

    private static async Task<FormaDto> Atualizar(HttpClient client, FormaDto f, Guid? contaId)
    {
        var r = await client.PutAsJsonAsync(U($"/api/vendas/formas/{f.Id}"),
            new { f.Nome, f.Tipo, contaId, f.TaxaAVista, f.TaxaParcelado, f.MaxParcelas, f.PrazoDias }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.OK, await r.Content.ReadAsStringAsync(Ct));
        return (await r.Content.ReadFromJsonAsync<FormaDto>(Ct))!;
    }

    private async Task<Cenario> Preparar()
    {
        var client = await Entrar(_api);
        var camiseta = await CriarCamiseta(client);
        var loja = (await Lojas(client)).Single();
        var azulM = Sku(camiseta, "Azul", "M").Id;
        await Entrada(client, loja.Id, new { skuId = azulM, quantidade = 5, custoUnitario = 20m });
        var r = await client.PostAsJsonAsync(U("/api/financeiro/contas"), new { nome = "Caixa", tipo = "Caixa", saldoInicial = 100m, saldoInicialEm = Hoje.AddDays(-5) }, Ct);
        var caixa = (await r.Content.ReadFromJsonAsync<ContaDto>(Ct))!;
        (await client.PostAsync(U("/api/vendas/formas/padrao"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var formas = await Formas(client);
        formas["Dinheiro"] = await Atualizar(client, formas["Dinheiro"], caixa.Id);
        formas["PIX"] = await Atualizar(client, formas["PIX"], caixa.Id);
        var v = await client.PostAsJsonAsync(U("/api/vendedores"), new { nome = "Bia", comissaoPercentual = 5m }, Ct);
        v.StatusCode.ShouldBe(HttpStatusCode.Created, await v.Content.ReadAsStringAsync(Ct));
        return new Cenario(client, camiseta, loja.Id, azulM, Sku(camiseta, "Preto", "G").Id, caixa, formas, (await v.Content.ReadFromJsonAsync<VendedorDto>(Ct))!);
    }

    private static object Pag(FormaDto forma, decimal valor, int parcelas = 1) => new { formaId = forma.Id, valor, parcelas };

    private static Task<HttpResponseMessage> Vender(HttpClient client, Guid lojaId, object[] itens, object[] pagamentos, Guid? vendedorId = null,
        decimal descontoGeral = 0, DateOnly? data = null, string? clienteNome = null) =>
        client.PostAsJsonAsync(U("/api/vendas"), new { lojaId, data, clienteNome, vendedorId, itens, descontoGeral, pagamentos, notaExterna = "NFC-e 991" }, Ct);

    private static async Task<VendaDto> VenderOk(HttpClient client, Guid lojaId, object[] itens, object[] pagamentos, Guid? vendedorId = null, decimal descontoGeral = 0)
    {
        var r = await Vender(client, lojaId, itens, pagamentos, vendedorId, descontoGeral);
        r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync(Ct));
        return (await r.Content.ReadFromJsonAsync<VendaDto>(Ct))!;
    }

    [Fact]
    public async Task Formas_PadraoUmaVez_ContaDaMesmaEmpresa_SoGestaoAltera()
    {
        var client = await Entrar(_api);
        (await client.PostAsync(U("/api/vendas/formas/padrao"), null, Ct)).EnsureSuccessStatusCode();
        (await client.PostAsync(U("/api/vendas/formas/padrao"), null, Ct)).EnsureSuccessStatusCode();

        var formas = await Formas(client);
        formas.Keys.Order().ShouldBe(["Cartão de crédito", "Cartão de débito", "Crediário", "Dinheiro", "PIX", "Vale-troca"]);
        formas["Cartão de crédito"].ShouldBe(formas["Cartão de crédito"] with { Tipo = "Credito", TaxaAVista = 3.5m, TaxaParcelado = 5.5m, MaxParcelas = 12, PrazoDias = 30, Ativa = true });

        var outra = await Entrar(_api, empresa: "Kamus Moda Shopping");
        var contaDeOutra = (await (await outra.PostAsJsonAsync(U("/api/financeiro/contas"), new { nome = "Caixa", tipo = "Caixa", saldoInicial = 0m, saldoInicialEm = Hoje }, Ct))
            .Content.ReadFromJsonAsync<ContaDto>(Ct))!;
        var f = formas["Dinheiro"];
        var r = await client.PutAsJsonAsync(U($"/api/vendas/formas/{f.Id}"), new { f.Nome, f.Tipo, contaId = contaDeOutra.Id, f.TaxaAVista, f.TaxaParcelado, f.MaxParcelas, f.PrazoDias }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await r.Content.ReadAsStringAsync(Ct)).ShouldContain("conta financeira");

        var repetida = await client.PostAsJsonAsync(U("/api/vendas/formas"), new { nome = "pix", tipo = "Pix", taxaAVista = 0m, taxaParcelado = 0m, maxParcelas = 1, prazoDias = 0 }, Ct);
        repetida.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var vendedora = await Entrar(_api, "vendedor@kamus.local");
        (await vendedora.GetAsync(U("/api/vendas/formas"), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await vendedora.PostAsync(U("/api/vendas/formas/padrao"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await vendedora.PostAsync(U($"/api/vendas/formas/{f.Id}/desativar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await vendedora.PutAsJsonAsync(U("/api/vendas/configuracao"), new { permitirEstoqueNegativo = false }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Registrar_DinheiroComTrocoECreditoParcelado_BaixaEstoqueLancaFinanceiroEComissao()
    {
        var c = await Preparar();

        var venda = await VenderOk(c.Client, c.LojaId, [new { skuId = c.AzulM, quantidade = 2, desconto = 0m }],
            [Pag(c.Formas["Dinheiro"], 60m), Pag(c.Formas["Cartão de crédito"], 60m, 2)], c.Bia.Id, descontoGeral: 9.80m);

        venda.Numero.ShouldBe(1);
        (venda.Subtotal, venda.DescontoGeral, venda.Total, venda.Troco, venda.Pecas).ShouldBe((119.80m, 9.80m, 110m, 10m, 2));
        (venda.VendedorNome, venda.Comissao, venda.Status, venda.NotaExterna).ShouldBe(("Bia", 5.50m, "Concluida", "NFC-e 991"));
        venda.Pagamentos.ShouldBe([new PagamentoDto("Dinheiro", "Dinheiro", 50m, 1, 0m, 0m), new PagamentoDto("Cartão de crédito", "Credito", 60m, 2, 5.5m, 3.30m)]);
        venda.Itens.Single().ShouldBe(new ItemDto(c.AzulM, venda.Itens[0].Descricao, 2, 59.90m, 0m, 119.80m, 20m));
        venda.Itens[0].Descricao.ShouldContain("Azul M");
        venda.LucroBruto.ShouldBe(110m - 40m - 3.30m);
        venda.Avisos.ShouldBeEmpty();

        (await Saldo(c.Client, c.Camiseta.Id, c.AzulM)).ShouldBe(3);
        (await c.Client.GetFromJsonAsync<MatrizDto>(U($"/api/estoque/posicao/{c.Camiseta.Id}"), Ct))!.Celulas.ShouldAllBe(x => x.Preco == 59.90m);
        (await SaldoDaConta(c.Client, c.Caixa.Id)).ShouldBe(150m);
        var receber = (await c.Client.GetFromJsonAsync<ListaReceberDto>(U("/api/financeiro/titulos?tipo=receber&situacao=abertas"), Ct))!.Itens;
        receber.Select(l => (l.Vencimento, l.Valor)).ShouldBe([(Hoje.AddDays(30), 28.35m), (Hoje.AddDays(60), 28.35m)]);
        receber[0].Descricao.ShouldContain("Venda nº 1");

        var segunda = await VenderOk(c.Client, c.LojaId, [new { skuId = c.AzulM, quantidade = 1, desconto = 9.90m }], [Pag(c.Formas["PIX"], 50m)]);
        (segunda.Numero, segunda.Total, segunda.Comissao).ShouldBe((2, 50m, 0m));
        (await c.Client.GetFromJsonAsync<VendaDto>(U($"/api/vendas/{venda.Id}"), Ct))!.Total.ShouldBe(110m);
    }

    [Fact]
    public async Task Registrar_SemSaldo_PermiteComAvisoOuRecusaConformeAConfiguracao()
    {
        var c = await Preparar();
        (await c.Client.GetFromJsonAsync<ConfiguracaoDto>(U("/api/vendas/configuracao"), Ct))!.PermitirEstoqueNegativo.ShouldBeTrue();

        var venda = await VenderOk(c.Client, c.LojaId, [new { skuId = c.PretoG, quantidade = 1, desconto = 0m }], [Pag(c.Formas["Dinheiro"], 59.90m)]);
        venda.Avisos.Single().ShouldContain("-1");
        (await Saldo(c.Client, c.Camiseta.Id, c.PretoG)).ShouldBe(-1);

        (await c.Client.PutAsJsonAsync(U("/api/vendas/configuracao"), new { permitirEstoqueNegativo = false }, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var recusada = await Vender(c.Client, c.LojaId, [new { skuId = c.AzulM, quantidade = 6, desconto = 0m }], [Pag(c.Formas["Dinheiro"], 359.40m)]);
        recusada.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await recusada.Content.ReadAsStringAsync(Ct)).ShouldContain("há 5");
        (await Saldo(c.Client, c.Camiseta.Id, c.AzulM)).ShouldBe(5);
        (await SaldoDaConta(c.Client, c.Caixa.Id)).ShouldBe(159.90m);
    }

    [Fact]
    public async Task Registrar_Invalida_NaoMexeEmNada()
    {
        var c = await Preparar();
        var item = new[] { new { skuId = c.AzulM, quantidade = 1, desconto = 0m } };

        async Task Recusa(HttpResponseMessage r, string trecho)
        {
            r.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await r.Content.ReadAsStringAsync(Ct)).ShouldContain(trecho);
        }

        await Recusa(await Vender(c.Client, c.LojaId, item, [Pag(c.Formas["PIX"], 50m)]), "faltam R$ 9,90");
        await Recusa(await Vender(c.Client, c.LojaId, item, [Pag(c.Formas["Cartão de crédito"], 59.90m, 13)]), "12");
        await Recusa(await Vender(c.Client, c.LojaId, item, [Pag(c.Formas["PIX"], 70m)]), "troco");
        await Recusa(await Vender(c.Client, c.LojaId, item, [Pag(c.Formas["PIX"], 59.90m)], data: Hoje.AddDays(1)), "futura");
        await Recusa(await Vender(c.Client, c.LojaId, [new { skuId = Guid.NewGuid(), quantidade = 1, desconto = 0m }], [Pag(c.Formas["PIX"], 59.90m)]), "catálogo");
        await Recusa(await Vender(c.Client, c.LojaId, item, [Pag(c.Formas["PIX"], 59.90m)], vendedorId: Guid.NewGuid()), "Vendedor");

        (await c.Client.PostAsync(U($"/api/vendas/formas/{c.Formas["Dinheiro"].Id}/desativar"), null, Ct)).EnsureSuccessStatusCode();
        await Recusa(await Vender(c.Client, c.LojaId, item, [Pag(c.Formas["Dinheiro"], 59.90m)]), "desativada");
        var semConta = await Atualizar(c.Client, c.Formas["PIX"], null);
        await Recusa(await Vender(c.Client, c.LojaId, item, [Pag(semConta, 59.90m)]), "conta");

        (await Saldo(c.Client, c.Camiseta.Id, c.AzulM)).ShouldBe(5);
        (await SaldoDaConta(c.Client, c.Caixa.Id)).ShouldBe(100m);
        (await c.Client.GetFromJsonAsync<ListaDto>(U("/api/vendas"), Ct))!.Itens.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cancelar_DevolveEstoqueEEstornaFinanceiro_SoGestao()
    {
        var c = await Preparar();
        var venda = await VenderOk(c.Client, c.LojaId, [new { skuId = c.AzulM, quantidade = 2, desconto = 0m }],
            [Pag(c.Formas["Dinheiro"], 59.80m), Pag(c.Formas["Cartão de débito"], 60m)], c.Bia.Id);

        var vendedora = await Entrar(_api, "vendedor@kamus.local");
        (await vendedora.PostAsJsonAsync(U($"/api/vendas/{venda.Id}/cancelar"), new { motivo = "Desistiu" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await c.Client.PostAsJsonAsync(U($"/api/vendas/{venda.Id}/cancelar"), new { motivo = " " }, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var r = await c.Client.PostAsJsonAsync(U($"/api/vendas/{venda.Id}/cancelar"), new { motivo = "Cliente desistiu" }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.OK, await r.Content.ReadAsStringAsync(Ct));
        var cancelada = (await r.Content.ReadFromJsonAsync<VendaDto>(Ct))!;
        (cancelada.Status, cancelada.MotivoDoCancelamento).ShouldBe(("Cancelada", "Cliente desistiu"));

        (await Saldo(c.Client, c.Camiseta.Id, c.AzulM)).ShouldBe(5);
        (await SaldoDaConta(c.Client, c.Caixa.Id)).ShouldBe(100m);
        (await c.Client.GetFromJsonAsync<ListaReceberDto>(U("/api/financeiro/titulos?tipo=receber&situacao=abertas"), Ct))!.Itens.ShouldBeEmpty();
        (await c.Client.PostAsJsonAsync(U($"/api/vendas/{venda.Id}/cancelar"), new { motivo = "De novo" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var lista = (await c.Client.GetFromJsonAsync<ListaDto>(U("/api/vendas?status=todas"), Ct))!;
        (lista.Itens.Single().Status, lista.Quantidade, lista.Total).ShouldBe(("Cancelada", 0, 0m));
        (await c.Client.GetFromJsonAsync<List<ComissaoDto>>(U($"/api/vendas/comissoes?de={Hoje:yyyy-MM-dd}&ate={Hoje:yyyy-MM-dd}"), Ct))!.ShouldBeEmpty();
    }

    [Fact]
    public async Task Listar_VendedorVeSoAsSuas_ComissoesPorVendedor_Relatorios()
    {
        var c = await Preparar();
        await VenderOk(c.Client, c.LojaId, [new { skuId = c.AzulM, quantidade = 1, desconto = 0m }], [Pag(c.Formas["PIX"], 59.90m)], c.Bia.Id);
        await VenderOk(c.Client, c.LojaId, [new { skuId = c.AzulM, quantidade = 2, desconto = 19.80m }], [Pag(c.Formas["Dinheiro"], 100m)], c.Bia.Id);
        var vendedora = await Entrar(_api, "vendedor@kamus.local");
        var r = await Vender(vendedora, c.LojaId, [new { skuId = c.AzulM, quantidade = 1, desconto = 0m }], [Pag(c.Formas["Dinheiro"], 59.90m)]);
        r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync(Ct));
        var daVendedora = (await r.Content.ReadFromJsonAsync<VendaDto>(Ct))!;
        daVendedora.LucroBruto.ShouldBeNull();

        var todas = (await c.Client.GetFromJsonAsync<ListaDto>(U($"/api/vendas?de={Hoje:yyyy-MM-dd}&ate={Hoje:yyyy-MM-dd}"), Ct))!;
        (todas.Quantidade, todas.Pecas, todas.Total, todas.TicketMedio).ShouldBe((3, 4, 219.80m, 73.27m));
        todas.Itens.Select(i => i.Numero).ShouldBe([3, 2, 1]);
        todas.Itens[2].Formas.ShouldBe("PIX");
        (await c.Client.GetFromJsonAsync<ListaDto>(U($"/api/vendas?vendedorId={c.Bia.Id}"), Ct))!.Quantidade.ShouldBe(2);

        var minhas = (await vendedora.GetFromJsonAsync<ListaDto>(U("/api/vendas"), Ct))!;
        minhas.Itens.Single().Id.ShouldBe(daVendedora.Id);
        (await vendedora.GetAsync(U($"/api/vendas/{todas.Itens[2].Id}"), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await vendedora.GetAsync(U("/api/vendas/comissoes"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var comissoes = (await c.Client.GetFromJsonAsync<List<ComissaoDto>>(U($"/api/vendas/comissoes?de={Hoje:yyyy-MM-dd}&ate={Hoje:yyyy-MM-dd}"), Ct))!;
        comissoes.ShouldBe([new ComissaoDto(c.Bia.Id, "Bia", 2, 159.90m, 8.00m), new ComissaoDto(null, "Sem vendedor", 1, 59.90m, 0m)]);

        var csv = await c.Client.GetStringAsync(U($"/api/relatorios/vendas.csv?de={Hoje:yyyy-MM-dd}&ate={Hoje:yyyy-MM-dd}"), Ct);
        csv.ShouldContain("Bia");
        csv.ShouldContain("PIX");
        (await c.Client.GetStringAsync(U($"/api/relatorios/comissoes.csv?de={Hoje:yyyy-MM-dd}&ate={Hoje:yyyy-MM-dd}"), Ct)).ShouldContain("Sem vendedor");
    }

    [Fact]
    public async Task Listar_ComPagina_DevolveAPaginaComTotaisDeTodas_SemPaginaDevolveTudo()
    {
        var c = await Preparar();
        foreach (var cliente in new[] { "Ana", "Bruna", "Carla" })
            (await Vender(c.Client, c.LojaId, [new { skuId = c.AzulM, quantidade = 1, desconto = 0m }], [Pag(c.Formas["PIX"], 59.90m)], clienteNome: cliente))
                .StatusCode.ShouldBe(HttpStatusCode.Created);

        var pagina = (await c.Client.GetFromJsonAsync<PaginaDeVendasDto>(U("/api/vendas?status=todas&pagina=2&tamanho=2"), Ct))!;
        pagina.Itens.Select(i => i.Numero).ShouldBe([1]);
        (pagina.Registros, pagina.Pagina, pagina.Tamanho, pagina.Quantidade, pagina.Pecas, pagina.Total).ShouldBe((3, 2, 2, 3, 3, 179.70m));

        var tudo = (await c.Client.GetFromJsonAsync<PaginaDeVendasDto>(U("/api/vendas"), Ct))!;
        (tudo.Itens.Count, tudo.Registros).ShouldBe((3, 3));
    }

    [Fact]
    public async Task Relatorio_SoAsVendasSelecionadas_IgnoraOsFiltros()
    {
        var c = await Preparar();
        var ids = new List<Guid>();
        foreach (var cliente in new[] { "Ana", "Bruna", "Carla" })
        {
            var r = await Vender(c.Client, c.LojaId, [new { skuId = c.AzulM, quantidade = 1, desconto = 0m }], [Pag(c.Formas["PIX"], 59.90m)], clienteNome: cliente);
            ids.Add((await r.Content.ReadFromJsonAsync<VendaDto>(Ct))!.Id);
        }

        var csv = await c.Client.GetStringAsync(U($"/api/relatorios/vendas.csv?ids={ids[0]},{ids[2]}&status=canceladas"), Ct);
        csv.ShouldContain("Ana");
        csv.ShouldContain("Carla");
        csv.ShouldNotContain("Bruna");
        var dados = await c.Client.GetFromJsonAsync<JsonElement>(U($"/api/relatorios/vendas/dados?ids={ids[1]}"), Ct);
        dados.GetProperty("linhas").GetArrayLength().ShouldBe(1);
        dados.GetProperty("filtros")[0].GetProperty("valor").GetString().ShouldBe("1 venda");
    }

    [Fact]
    public async Task Formas_PaginadasComFiltro_SituacaoEmLote_RelatorioDasSelecionadas_SoGestao()
    {
        var client = await Entrar(_api);
        (await client.PostAsync(U("/api/vendas/formas/padrao"), null, Ct)).EnsureSuccessStatusCode();
        var formas = await Formas(client);

        var pagina = (await client.GetFromJsonAsync<PaginaDto<FormaDto>>(U("/api/vendas/formas?pagina=1&tamanho=4"), Ct))!;
        (pagina.Itens.Count, pagina.Total, pagina.Pagina, pagina.Tamanho).ShouldBe((4, 6, 1, 4));
        (await client.GetFromJsonAsync<PaginaDto<FormaDto>>(U("/api/vendas/formas?busca=cartao&pagina=1&tamanho=20"), Ct))!.Total.ShouldBe(2);

        var ids = new[] { formas["Dinheiro"].Id, formas["PIX"].Id };
        var lote = await client.PostAsJsonAsync(U("/api/vendas/formas/situacao"), new { ids, ativo = false }, Ct);
        lote.StatusCode.ShouldBe(HttpStatusCode.OK, await lote.Content.ReadAsStringAsync(Ct));
        (await lote.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("alterados").GetInt32().ShouldBe(2);
        var inativas = (await client.GetFromJsonAsync<PaginaDto<FormaDto>>(U("/api/vendas/formas?situacao=inativas&pagina=1&tamanho=20"), Ct))!;
        inativas.Itens.Select(f => f.Nome).Order().ShouldBe(["Dinheiro", "PIX"]);
        (await client.GetFromJsonAsync<PaginaDto<FormaDto>>(U("/api/vendas/formas?situacao=ativas&pagina=1&tamanho=20"), Ct))!.Total.ShouldBe(4);
        (await client.PostAsJsonAsync(U("/api/vendas/formas/situacao"), new { ids = new[] { formas["PIX"].Id }, ativo = true }, Ct)).EnsureSuccessStatusCode();
        (await Formas(client))["PIX"].Ativa.ShouldBeTrue();
        (await client.PostAsJsonAsync(U("/api/vendas/formas/situacao"), new { ids = Array.Empty<Guid>(), ativo = true }, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var csv = await client.GetStringAsync(U($"/api/relatorios/formas-de-pagamento.csv?ids={formas["PIX"].Id}"), Ct);
        csv.ShouldContain("PIX");
        csv.ShouldNotContain("Dinheiro");
        (await client.GetStringAsync(U("/api/relatorios/formas-de-pagamento.csv?situacao=inativas"), Ct)).ShouldContain("Dinheiro");

        var vendedora = await Entrar(_api, "vendedor@kamus.local");
        (await vendedora.PostAsJsonAsync(U("/api/vendas/formas/situacao"), new { ids, ativo = true }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
