using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kamus.UnitTests.Estoque;
using static Kamus.IntegrationTests.Estoque.ApiDoEstoque;

namespace Kamus.IntegrationTests.Estoque;

/// <summary>Entrada por XML da NF-e: importar, conferir (EAN, de-para, grade, fator), lançar no estoque e lembrar o de-para.</summary>
public sealed class NotasTests : IAsyncDisposable
{
    private readonly Infra.KamusApiFactory _api = NovaApi();

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    private sealed record DistribuicaoDto(Guid SkuId, string Cor, string Tamanho, int Quantidade);
    private sealed record ItemDto(int Numero, string Codigo, string? Ean, string Descricao, decimal Quantidade, decimal CustoTotal, decimal CustoPorPeca, int Fator,
        decimal PecasEsperadas, string Situacao, string? OrigemDoVinculo, Guid? ProdutoId, List<DistribuicaoDto> Distribuicao);
    private sealed record DuplicataDto(string Numero, DateOnly Vencimento, decimal Valor);
    private sealed record NotaDto(Guid Id, string Chave, string Numero, string FornecedorDocumento, string FornecedorNome, bool FornecedorCadastrado, decimal ValorTotal,
        bool Autorizada, string Status, bool ProntaParaLancar, Guid? OperacaoId, List<ItemDto> Itens, List<DuplicataDto> Duplicatas);
    private sealed record ResumoDto(Guid Id, string Numero, string FornecedorNome, string Status, int Itens, int Pendentes);
    private sealed record CelulaDto(Guid SkuId, int Quantidade, decimal? CustoMedio);
    private sealed record MatrizDto(List<CelulaDto> Celulas);
    private sealed record MovimentoDto(string Tipo, int Quantidade, string? Documento);

    private static readonly ExemploDeNfe.Item Camisetas = new("AUR-100", "SEM GTIN", "CAMISETA BASICA SORTIDA", "UN", 10, 25m, Frete: 10m);
    private static readonly ExemploDeNfe.Item ComEan = new("AUR-200", "7891234567895", "CAMISETA PRETA G", "UN", 3, 30m);
    private static readonly ExemploDeNfe.Item Embalagem = new("EMB-1", "SEM GTIN", "SACOLA PERSONALIZADA", "UN", 50, 0.5m);

    private static async Task<HttpResponseMessage> Importar(HttpClient client, string xml, string nome = "nota.xml")
    {
        using var form = new MultipartFormDataContent();
        var conteudo = new ByteArrayContent(Encoding.UTF8.GetBytes(xml));
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
        form.Add(conteudo, "arquivo", nome);
        return await client.PostAsync(U("/api/estoque/notas"), form, Ct);
    }

    private static async Task<NotaDto> ImportarOk(HttpClient client, string xml)
    {
        var resposta = await Importar(client, xml);
        resposta.StatusCode.ShouldBe(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync(Ct));
        return (await resposta.Content.ReadFromJsonAsync<NotaDto>(Ct))!;
    }

    private static Task<HttpResponseMessage> Item(HttpClient client, Guid notaId, int numero, object corpo) =>
        client.PutAsJsonAsync(U($"/api/estoque/notas/{notaId}/itens/{numero}"), corpo, Ct);

    private static async Task<ProdutoDto> CamisetaComEan(HttpClient client)
    {
        var camiseta = await CriarCamiseta(client);
        var pretoG = Sku(camiseta, "Preto", "G");
        (await client.PutAsJsonAsync(U($"/api/produtos/{camiseta.Id}/skus/{pretoG.Id}"), new { codigoDeBarras = "7891234567895", ativo = true }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        return camiseta;
    }

    [Fact]
    public async Task FluxoCompleto_EanGradeIgnorarELancar()
    {
        var client = await Entrar(_api);
        var camiseta = await CamisetaComEan(client);
        var loja = (await Lojas(client)).Single();
        var (azulP, azulM, pretoG) = (Sku(camiseta, "Azul", "P"), Sku(camiseta, "Azul", "M"), Sku(camiseta, "Preto", "G"));

        var nota = await ImportarOk(client, ExemploDeNfe.Xml([Camisetas, ComEan, Embalagem]));

        nota.ShouldSatisfyAllConditions(
            () => nota.Numero.ShouldBe("1234"),
            () => nota.FornecedorNome.ShouldBe("Aurora Moda"),
            () => nota.FornecedorCadastrado.ShouldBeFalse(),
            () => nota.Autorizada.ShouldBeTrue(),
            () => nota.Status.ShouldBe("Pendente"),
            () => nota.Duplicatas.Count.ShouldBe(2),
            () => nota.Itens.Select(i => (i.Codigo, i.Situacao, i.OrigemDoVinculo)).ShouldBe(
                [("AUR-100", "Pendente", null), ("AUR-200", "Pronto", "ean"), ("EMB-1", "Pendente", null)]));
        nota.Itens[1].Distribuicao.Single().ShouldBe(new DistribuicaoDto(pretoG.Id, "Preto", "G", 3));
        (await client.GetFromJsonAsync<List<ResumoDto>>(U("/api/estoque/notas"), Ct))!.Single().ShouldBe(new ResumoDto(nota.Id, "1234", "Aurora Moda", "Pendente", 3, 2));
        (await client.PostAsJsonAsync(U($"/api/estoque/notas/{nota.Id}/lancar"), new { lojaId = loja.Id }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var errado = await Item(client, nota.Id, 1, new { produtoId = camiseta.Id, fator = 1, distribuicao = new[] { new { skuId = azulP.Id, quantidade = 4 } } });
        errado.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await errado.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("detail").GetString()!.ShouldContain("soma 4");
        var certo = await Item(client, nota.Id, 1, new { produtoId = camiseta.Id, fator = 1, distribuicao = new[] { new { skuId = azulP.Id, quantidade = 4 }, new { skuId = azulM.Id, quantidade = 6 } } });
        certo.StatusCode.ShouldBe(HttpStatusCode.OK, await certo.Content.ReadAsStringAsync(Ct));
        (await certo.Content.ReadFromJsonAsync<ItemDto>(Ct))!.ShouldSatisfyAllConditions(i => i.Situacao.ShouldBe("Pronto"), i => i.CustoPorPeca.ShouldBe(26m));
        (await Item(client, nota.Id, 3, new { ignorado = true })).StatusCode.ShouldBe(HttpStatusCode.OK);

        var lancar = await client.PostAsJsonAsync(U($"/api/estoque/notas/{nota.Id}/lancar"), new { lojaId = loja.Id }, Ct);

        lancar.StatusCode.ShouldBe(HttpStatusCode.OK, await lancar.Content.ReadAsStringAsync(Ct));
        (await lancar.Content.ReadFromJsonAsync<OperacaoDto>(Ct))!.Quantidade.ShouldBe(13);
        var matriz = (await client.GetFromJsonAsync<MatrizDto>(U($"/api/estoque/posicao/{camiseta.Id}"), Ct))!;
        matriz.Celulas.Single(c => c.SkuId == azulM.Id).ShouldBe(new CelulaDto(azulM.Id, 6, 26m));
        matriz.Celulas.Single(c => c.SkuId == pretoG.Id).ShouldBe(new CelulaDto(pretoG.Id, 3, 30m));
        (await client.GetFromJsonAsync<List<MovimentoDto>>(U($"/api/estoque/kardex?skuId={azulP.Id}"), Ct))!.Single()
            .ShouldBe(new MovimentoDto("Entrada", 4, "NF 1234/1"));

        var lancada = (await client.GetFromJsonAsync<NotaDto>(U($"/api/estoque/notas/{nota.Id}"), Ct))!;
        (lancada.Status, lancada.FornecedorCadastrado, lancada.OperacaoId is not null).ShouldBe(("Lancada", true, true));
        var fornecedores = await client.GetStringAsync(U("/api/pessoas?papel=fornecedor"), Ct);
        fornecedores.ShouldContain("Confecções Aurora LTDA");
        (await Importar(client, ExemploDeNfe.Xml([Camisetas]))).StatusCode.ShouldBe(HttpStatusCode.Conflict); // mesma chave
        (await client.GetAsync(U($"/api/estoque/etiquetas/operacao/{lancada.OperacaoId}"), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DePara_ProximaNotaDoMesmoFornecedorVinculaSozinha()
    {
        var client = await Entrar(_api);
        var camiseta = await CriarCamiseta(client);
        var loja = (await Lojas(client)).Single();
        var (azulP, azulM) = (Sku(camiseta, "Azul", "P"), Sku(camiseta, "Azul", "M"));
        var umSku = new ExemploDeNfe.Item("AUR-300", "SEM GTIN", "CAMISETA AZUL P", "CX", 2, 120m);
        var primeira = await ImportarOk(client, ExemploDeNfe.Xml([Camisetas, umSku]));
        await Item(client, primeira.Id, 1, new { produtoId = camiseta.Id, fator = 1, distribuicao = new[] { new { skuId = azulP.Id, quantidade = 5 }, new { skuId = azulM.Id, quantidade = 5 } } });
        (await Item(client, primeira.Id, 2, new { produtoId = camiseta.Id, fator = 12, distribuicao = new[] { new { skuId = azulP.Id, quantidade = 24 } } }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync(U($"/api/estoque/notas/{primeira.Id}/lancar"), new { lojaId = loja.Id }, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var segunda = await ImportarOk(client, ExemploDeNfe.Xml([Camisetas, umSku with { Quantidade = 1 }], chave: "35261011222333000181550010000099991000099990", numero: "9999"));

        segunda.Itens[0].ShouldSatisfyAllConditions(i => i.Situacao.ShouldBe("Pendente"), i => i.ProdutoId.ShouldBe(camiseta.Id), i => i.OrigemDoVinculo.ShouldBe("de-para"));
        segunda.Itens[1].ShouldSatisfyAllConditions(i => i.Situacao.ShouldBe("Pronto"), i => i.Fator.ShouldBe(12), i => i.PecasEsperadas.ShouldBe(12m),
            i => i.OrigemDoVinculo.ShouldBe("de-para"), i => i.CustoPorPeca.ShouldBe(10m));
    }

    [Fact]
    public async Task Recusas_OutroDestinatarioArquivoInvalidoENaoAutorizada()
    {
        var client = await Entrar(_api);

        var outro = await Importar(client, ExemploDeNfe.Xml([Camisetas], destinatario: "98765432000198"));
        outro.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await outro.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("detail").GetString()!.ShouldContain("98.765.432/0001-98");
        (await Importar(client, "não é xml")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Importar(client, ExemploDeNfe.Xml([Camisetas], cStat: "302"))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Importar(client, ExemploDeNfe.Xml([Camisetas]), "nota.pdf")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Descartar_PermiteImportarDeNovo()
    {
        var client = await Entrar(_api);
        var nota = await ImportarOk(client, ExemploDeNfe.Xml([Camisetas]));

        (await client.PostAsync(U($"/api/estoque/notas/{nota.Id}/descartar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var denovo = await ImportarOk(client, ExemploDeNfe.Xml([Camisetas]));

        denovo.Id.ShouldNotBe(nota.Id);
        (await client.GetFromJsonAsync<List<ResumoDto>>(U("/api/estoque/notas"), Ct))!.Select(n => n.Status).ShouldBe(["Pendente"]);
    }

    [Fact]
    public async Task OutraEmpresaNaoVeANota_VendedorNaoImporta()
    {
        var centro = await Entrar(_api);
        var nota = await ImportarOk(centro, ExemploDeNfe.Xml([Camisetas]));
        var shopping = await Entrar(_api, empresa: "Kamus Moda Shopping");
        var vendedor = await Entrar(_api, "vendedor@kamus.local");

        (await shopping.GetAsync(U($"/api/estoque/notas/{nota.Id}"), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await shopping.GetFromJsonAsync<List<ResumoDto>>(U("/api/estoque/notas"), Ct))!.ShouldBeEmpty();
        (await Importar(vendedor, ExemploDeNfe.Xml([Camisetas]))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
