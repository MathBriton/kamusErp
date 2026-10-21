using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kamus.SharedKernel.Tempo;
using static Kamus.IntegrationTests.Estoque.ApiDoEstoque;

namespace Kamus.IntegrationTests.Vendas;

/// <summary>Condicional (malinha): saída do estoque, limites e bloqueio por atraso, termo em PDF, fechamento devolvendo parte e vendendo o resto, conversão.</summary>
public sealed class CondicionaisTests : IAsyncDisposable
{
    private readonly Infra.KamusApiFactory _api = NovaApi();
    private static readonly DateOnly Hoje = CalendarioBrasilia.Hoje(TimeProvider.System);

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    private sealed record FormaDto(Guid Id, string Nome, string Tipo, Guid? ContaId, decimal TaxaAVista, decimal TaxaParcelado, int MaxParcelas, int PrazoDias);
    private sealed record PessoaDto(Guid Id, string Nome);
    private sealed record ContaDto(Guid Id, decimal Saldo);
    private sealed record VendedorDto(Guid Id, string Nome);
    private sealed record ItemDto(Guid SkuId, string Descricao, int Quantidade, decimal PrecoUnitario, int Devolvidas);
    private sealed record CondicionalDto(Guid Id, int Numero, DateOnly Data, DateOnly Prazo, string ClienteNome, string? VendedorNome, string Situacao, int Pecas,
        decimal Valor, List<ItemDto> Itens, int? VendaNumero);
    private sealed record FechamentoDto(CondicionalDto Condicional, int? VendaNumero, decimal? VendaTotal);
    private sealed record LinhaDto(Guid Id, int Numero, string ClienteNome, int Pecas, decimal Valor, string Situacao);
    private sealed record ListaDto(List<LinhaDto> Itens, int Abertas, int Atrasadas, int PecasNaRua, decimal ValorNaRua);
    private sealed record PaginaDto(List<LinhaDto> Itens, int Total, int Pagina, int Tamanho, int Abertas, int PecasNaRua);
    private sealed record ConversaoDto(Guid? VendedorId, string Vendedor, int Condicionais, int PecasLevadas, int PecasVendidas, decimal ValorLevado,
        decimal ValorVendido, decimal Conversao);
    private sealed record CelulaDto(Guid SkuId, int Quantidade);
    private sealed record MatrizDto(List<CelulaDto> Celulas);
    private sealed record MovimentoDto(string Tipo, int Quantidade, string? Documento);

    private sealed record Cenario(HttpClient Client, ProdutoDto Camiseta, Guid LojaId, Guid AzulM, Guid AzulG, PessoaDto Maria, FormaDto Pix, VendedorDto Bia);

    private async Task<Cenario> Preparar()
    {
        var client = await Entrar(_api);
        var camiseta = await CriarCamiseta(client);
        var loja = (await Lojas(client)).Single();
        var (azulM, azulG) = (Sku(camiseta, "Azul", "M").Id, Sku(camiseta, "Azul", "G").Id);
        await Entrada(client, loja.Id, new { skuId = azulM, quantidade = 5, custoUnitario = 20m }, new { skuId = azulG, quantidade = 5, custoUnitario = 20m });
        var caixa = (await (await client.PostAsJsonAsync(U("/api/financeiro/contas"), new { nome = "Caixa", tipo = "Caixa", saldoInicial = 0m, saldoInicialEm = Hoje.AddDays(-30) }, Ct))
            .Content.ReadFromJsonAsync<ContaDto>(Ct))!;
        (await client.PostAsync(U("/api/vendas/formas/padrao"), null, Ct)).EnsureSuccessStatusCode();
        var pix = (await client.GetFromJsonAsync<List<FormaDto>>(U("/api/vendas/formas"), Ct))!.Single(f => f.Tipo == "Pix");
        (await client.PutAsJsonAsync(U($"/api/vendas/formas/{pix.Id}"), new { pix.Nome, pix.Tipo, contaId = caixa.Id, pix.TaxaAVista, pix.TaxaParcelado, pix.MaxParcelas, pix.PrazoDias }, Ct))
            .EnsureSuccessStatusCode();
        var maria = (await (await client.PostAsJsonAsync(U("/api/pessoas"), new { tipo = "Fisica", nome = "Maria Souza", ehCliente = true, ehFornecedor = false, aceitaMarketing = false }, Ct))
            .Content.ReadFromJsonAsync<PessoaDto>(Ct))!;
        var bia = (await (await client.PostAsJsonAsync(U("/api/vendedores"), new { nome = "Bia", comissaoPercentual = 5m }, Ct)).Content.ReadFromJsonAsync<VendedorDto>(Ct))!;
        return new Cenario(client, camiseta, loja.Id, azulM, azulG, maria, pix, bia);
    }

    private static Task<HttpResponseMessage> Abrir(HttpClient client, Cenario c, object[] itens, Guid? clienteId = null, bool liberar = false, DateOnly? data = null,
        DateOnly? prazo = null) =>
        client.PostAsJsonAsync(U("/api/vendas/condicionais"), new
        {
            lojaId = c.LojaId,
            clienteId = clienteId ?? c.Maria.Id,
            clienteNome = "Maria Souza",
            vendedorId = c.Bia.Id,
            data,
            prazo,
            itens,
            liberar,
        }, Ct);

    private static async Task<CondicionalDto> AbrirOk(HttpClient client, Cenario c, object[] itens, DateOnly? data = null, DateOnly? prazo = null, bool liberar = false)
    {
        var r = await Abrir(client, c, itens, liberar: liberar, data: data, prazo: prazo);
        r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync(Ct));
        return (await r.Content.ReadFromJsonAsync<CondicionalDto>(Ct))!;
    }

    private static async Task<int> Saldo(Cenario c, Guid skuId) =>
        (await c.Client.GetFromJsonAsync<MatrizDto>(U($"/api/estoque/posicao/{c.Camiseta.Id}"), Ct))!.Celulas.Single(x => x.SkuId == skuId).Quantidade;

    private static async Task Recusa(HttpResponseMessage r, string trecho)
    {
        r.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await r.Content.ReadAsStringAsync(Ct)).ShouldContain(trecho);
    }

    [Fact]
    public async Task Abrir_TiraDoEstoque_ExigeCliente_LimitesEAtrasoSoGestaoLibera()
    {
        var c = await Preparar();
        var vendedora = await Entrar(_api, "vendedor@kamus.local");

        await Recusa(await Abrir(c.Client, c, [new { skuId = c.AzulM, quantidade = 1 }], clienteId: Guid.Empty), "cliente cadastrado");
        await Recusa(await Abrir(c.Client, c, [new { skuId = c.AzulM, quantidade = 6 }]), "há 5");

        (await c.Client.PutAsJsonAsync(U("/api/vendas/configuracao"), new { permitirEstoqueNegativo = true, limiteCondicionalPecas = 3 }, Ct)).EnsureSuccessStatusCode();
        await Recusa(await Abrir(vendedora, c, [new { skuId = c.AzulM, quantidade = 4 }], liberar: true), "limite de 3");
        var liberada = await AbrirOk(c.Client, c, [new { skuId = c.AzulM, quantidade = 4 }], liberar: true);
        (liberada.Numero, liberada.Pecas, liberada.Valor, liberada.Prazo, liberada.Situacao).ShouldBe((1, 4, 239.60m, Hoje.AddDays(2), "Aberta"));
        (await Saldo(c, c.AzulM)).ShouldBe(1);

        (await c.Client.PutAsJsonAsync(U("/api/vendas/configuracao"), new { permitirEstoqueNegativo = true, limiteCondicionalPecas = 0 }, Ct)).EnsureSuccessStatusCode();
        await AbrirOk(c.Client, c, [new { skuId = c.AzulG, quantidade = 1 }], data: Hoje.AddDays(-5), prazo: Hoje.AddDays(-3));
        await Recusa(await Abrir(vendedora, c, [new { skuId = c.AzulG, quantidade = 1 }]), "atrasada");
        await AbrirOk(c.Client, c, [new { skuId = c.AzulG, quantidade = 1 }], liberar: true);

        var lista = (await vendedora.GetFromJsonAsync<ListaDto>(U($"/api/vendas/condicionais?situacao=abertas&clienteId={c.Maria.Id}"), Ct))!;
        (lista.Itens.Count, lista.Abertas, lista.Atrasadas, lista.PecasNaRua, lista.ValorNaRua).ShouldBe((3, 3, 1, 6, 359.40m));
        (await c.Client.GetFromJsonAsync<ListaDto>(U("/api/vendas/condicionais?situacao=atrasadas"), Ct))!.Itens.Single().Numero.ShouldBe(2);
        (await c.Client.GetStringAsync(U("/api/relatorios/condicionais.csv?situacao=abertas"), Ct)).ShouldContain("Maria Souza");

        var pagina = (await c.Client.GetFromJsonAsync<PaginaDto>(U("/api/vendas/condicionais?situacao=abertas&pagina=1&tamanho=2"), Ct))!;
        (pagina.Itens.Count, pagina.Total, pagina.Pagina, pagina.Tamanho, pagina.Abertas, pagina.PecasNaRua).ShouldBe((2, 3, 1, 2, 3, 6));
        var dados = await c.Client.GetFromJsonAsync<JsonElement>(U($"/api/relatorios/condicionais/dados?ids={liberada.Id}&situacao=fechadas"), Ct);
        dados.GetProperty("linhas").GetArrayLength().ShouldBe(1);
        dados.GetProperty("linhas")[0][0].GetInt32().ShouldBe(liberada.Numero);
        dados.GetProperty("filtros")[0].GetProperty("valor").GetString().ShouldBe("1 condicional");
        var busca = await c.Client.GetFromJsonAsync<JsonElement>(U("/api/relatorios/condicionais/dados?situacao=todas&busca=2"), Ct);
        busca.GetProperty("linhas").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Fechar_DevolveParteEVendeOResto_TermoEmPdf_Conversao()
    {
        var c = await Preparar();
        var condicional = await AbrirOk(c.Client, c, [new { skuId = c.AzulM, quantidade = 2 }, new { skuId = c.AzulG, quantidade = 1 }]);

        var termo = await c.Client.GetAsync(U($"/api/vendas/condicionais/{condicional.Id}/termo.pdf"), Ct);
        termo.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        (await termo.Content.ReadAsByteArrayAsync(Ct))[..4].ShouldBe("%PDF"u8.ToArray());

        var semPagamento = await c.Client.PostAsJsonAsync(U($"/api/vendas/condicionais/{condicional.Id}/fechar"),
            new { devolvidos = new[] { new { skuId = c.AzulM, quantidade = 1 } }, pagamentos = Array.Empty<object>(), descontoGeral = 0m }, Ct);
        await Recusa(semPagamento, "forma de pagamento");
        ((await Saldo(c, c.AzulM)), (await Saldo(c, c.AzulG))).ShouldBe((3, 4)); // recusa não mexe no estoque

        var vendedora = await Entrar(_api, "vendedor@kamus.local");
        var r = await vendedora.PostAsJsonAsync(U($"/api/vendas/condicionais/{condicional.Id}/fechar"), new
        {
            devolvidos = new[] { new { skuId = c.AzulM, quantidade = 1 } },
            pagamentos = new[] { new { formaId = c.Pix.Id, valor = 119.80m, parcelas = 1 } },
            descontoGeral = 0m,
            notaExterna = "NFC-e 77",
        }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.OK, await r.Content.ReadAsStringAsync(Ct));
        var fechado = (await r.Content.ReadFromJsonAsync<FechamentoDto>(Ct))!;
        (fechado.VendaNumero, fechado.VendaTotal, fechado.Condicional.Situacao).ShouldBe((1, 119.80m, "Fechada"));
        fechado.Condicional.Itens.Select(i => (i.SkuId, i.Devolvidas)).ShouldBe([(c.AzulM, 1), (c.AzulG, 0)], ignoreOrder: true);

        ((await Saldo(c, c.AzulM)), (await Saldo(c, c.AzulG))).ShouldBe((4, 4));
        (await c.Client.GetFromJsonAsync<List<MovimentoDto>>(U($"/api/estoque/kardex?skuId={c.AzulM}"), Ct))!.Take(3).Select(m => (m.Tipo, m.Quantidade))
            .ShouldBe([("Venda", -1), ("RetornoDeCondicional", 2), ("SaidaParaCondicional", -2)]);
        (await c.Client.PostAsJsonAsync(U($"/api/vendas/condicionais/{condicional.Id}/fechar"), new { devolvidos = Array.Empty<object>(), descontoGeral = 0m }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var tudoDevolvido = await AbrirOk(c.Client, c, [new { skuId = c.AzulG, quantidade = 1 }]);
        var f2 = await c.Client.PostAsJsonAsync(U($"/api/vendas/condicionais/{tudoDevolvido.Id}/fechar"),
            new { devolvidos = new[] { new { skuId = c.AzulG, quantidade = 1 } }, descontoGeral = 0m }, Ct);
        f2.StatusCode.ShouldBe(HttpStatusCode.OK, await f2.Content.ReadAsStringAsync(Ct));
        (await f2.Content.ReadFromJsonAsync<FechamentoDto>(Ct))!.VendaNumero.ShouldBeNull();

        var conversao = (await c.Client.GetFromJsonAsync<List<ConversaoDto>>(U($"/api/vendas/condicionais/conversao?de={Hoje:yyyy-MM-dd}&ate={Hoje:yyyy-MM-dd}"), Ct))!;
        conversao.Single().ShouldBe(new ConversaoDto(c.Bia.Id, "Bia", 2, 4, 2, 239.60m, 119.80m, 50m));
        (await c.Client.GetFromJsonAsync<ListaDto>(U("/api/vendas/condicionais?situacao=abertas"), Ct))!.PecasNaRua.ShouldBe(0);
    }
}
