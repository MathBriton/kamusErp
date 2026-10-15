using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Kamus.IntegrationTests.Estoque.ApiDoEstoque;

namespace Kamus.IntegrationTests.Estoque;

/// <summary>Inventário: foto na abertura, contagem cega (matriz ou leitor), divergências e conclusão com ajuste pela diferença.</summary>
public sealed class InventarioTests : IAsyncDisposable
{
    private readonly Infra.KamusApiFactory _api = NovaApi();

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    private sealed record InventarioDto(Guid Id, Guid LojaId, string Loja, string Status, string? AbertoPor, int SkusContados, int PecasContadas);
    private sealed record CelulaDto(Guid SkuId, Guid CorId, string Tamanho, bool Ativo, int? Contado);
    private sealed record ContagemDto(Guid ProdutoId, List<string> Tamanhos, List<CelulaDto> Celulas);
    private sealed record LeituraDto(Guid SkuId, string Referencia, string Cor, string Tamanho, int Contado);
    private sealed record DivergenciaDto(Guid SkuId, string Referencia, string Cor, string Tamanho, int SaldoNaAbertura, int? Contado, int Diferenca, decimal? ValorDaDiferenca);
    private sealed record DivergenciasDto(List<DivergenciaDto> Itens, int Sobras, int Faltas, decimal? Valor, int NaoContados);
    private sealed record ConclusaoDto(Guid OperacaoId, int Ajustes, int Sobras, int Faltas);
    private sealed record CelulaDaPosicaoDto(Guid SkuId, int Quantidade);
    private sealed record MatrizDto(List<CelulaDaPosicaoDto> Celulas);
    private sealed record MovimentoDto(string Tipo, int Quantidade, string? Motivo);

    private static async Task<InventarioDto> Abrir(HttpClient client, Guid lojaId)
    {
        var resposta = await client.PostAsJsonAsync(U("/api/estoque/inventarios"), new { lojaId, observacao = "Balanço" }, Ct);
        resposta.StatusCode.ShouldBe(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync(Ct));
        return (await resposta.Content.ReadFromJsonAsync<InventarioDto>(Ct))!;
    }

    private static Task<HttpResponseMessage> Contar(HttpClient client, Guid id, params object[] itens) =>
        client.PutAsJsonAsync(U($"/api/estoque/inventarios/{id}/contagens"), new { itens }, Ct);

    private static async Task<int> Saldo(HttpClient client, Guid produtoId, Guid skuId) =>
        (await client.GetFromJsonAsync<MatrizDto>(U($"/api/estoque/posicao/{produtoId}"), Ct))!.Celulas.Single(c => c.SkuId == skuId).Quantidade;

    [Fact]
    public async Task FluxoCompleto_ContaDivergeEConcluiPelaDiferenca()
    {
        var client = await Entrar(_api);
        var camiseta = await CriarCamiseta(client);
        var loja = (await Lojas(client)).Single();
        var (azulM, pretoG, azulP) = (Sku(camiseta, "Azul", "M"), Sku(camiseta, "Preto", "G"), Sku(camiseta, "Azul", "P"));
        await Entrada(client, loja.Id, new { skuId = azulM.Id, quantidade = 5, custoUnitario = 20m }, new { skuId = pretoG.Id, quantidade = 3, custoUnitario = 20m });

        var inventario = await Abrir(client, loja.Id);
        inventario.Status.ShouldBe("Aberto");

        (await Contar(client, inventario.Id, new { skuId = azulM.Id, quantidade = 4 })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var leitura = await client.PostAsJsonAsync(U($"/api/estoque/inventarios/{inventario.Id}/leituras"), new { codigo = azulP.CodigoDeBarras }, Ct);
        await client.PostAsJsonAsync(U($"/api/estoque/inventarios/{inventario.Id}/leituras"), new { codigo = azulP.CodigoDeBarras }, Ct);
        leitura.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await leitura.Content.ReadFromJsonAsync<LeituraDto>(Ct))!.ShouldBe(new LeituraDto(azulP.Id, "CAM-001", "Azul", "P", 1));
        (await client.PostAsJsonAsync(U($"/api/estoque/inventarios/{inventario.Id}/leituras"), new { codigo = "7891234567895" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var contagem = (await client.GetFromJsonAsync<ContagemDto>(U($"/api/estoque/inventarios/{inventario.Id}/produtos/{camiseta.Id}"), Ct))!;
        contagem.Celulas.Single(c => c.SkuId == azulP.Id).Contado.ShouldBe(2);
        contagem.Celulas.Single(c => c.SkuId == pretoG.Id).Contado.ShouldBeNull(); // contagem cega: sem saldo, só o contado

        // Venda/entrada durante a contagem não se perde: entra 1 Azul M depois da foto.
        await Entrada(client, loja.Id, new { skuId = azulM.Id, quantidade = 1, custoUnitario = 20m });

        var divergencias = (await client.GetFromJsonAsync<DivergenciasDto>(U($"/api/estoque/inventarios/{inventario.Id}/divergencias?zerarNaoContados=true"), Ct))!;
        divergencias.Itens.Select(d => (d.Cor, d.Tamanho, d.SaldoNaAbertura, d.Contado, d.Diferenca)).ShouldBe(
            [("Azul", "M", 5, (int?)4, -1), ("Azul", "P", 0, 2, 2), ("Preto", "G", 3, null, -3)], ignoreOrder: true);
        (divergencias.Sobras, divergencias.Faltas, divergencias.NaoContados).ShouldBe((2, 4, 1));

        var concluir = await client.PostAsJsonAsync(U($"/api/estoque/inventarios/{inventario.Id}/concluir"), new { zerarNaoContados = true }, Ct);
        concluir.StatusCode.ShouldBe(HttpStatusCode.OK, await concluir.Content.ReadAsStringAsync(Ct));
        (await concluir.Content.ReadFromJsonAsync<ConclusaoDto>(Ct))!.ShouldSatisfyAllConditions(c => c.Ajustes.ShouldBe(3), c => c.Sobras.ShouldBe(2), c => c.Faltas.ShouldBe(4));

        (await Saldo(client, camiseta.Id, azulM.Id)).ShouldBe(5); // 6 (5 da foto + 1 depois) − 1
        (await Saldo(client, camiseta.Id, azulP.Id)).ShouldBe(2);
        (await Saldo(client, camiseta.Id, pretoG.Id)).ShouldBe(0);
        var kardex = (await client.GetFromJsonAsync<List<MovimentoDto>>(U($"/api/estoque/kardex?skuId={azulM.Id}"), Ct))!;
        kardex[0].ShouldBe(new MovimentoDto("Inventario", -1, "Inventário"));
        (await Contar(client, inventario.Id, new { skuId = azulM.Id, quantidade = 1 })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await client.GetStringAsync(U("/api/auditoria"), Ct)).ShouldContain("inventario_concluido");
    }

    [Fact]
    public async Task UmAbertoPorLoja_ECancelarLibera()
    {
        var client = await Entrar(_api);
        var loja = (await Lojas(client)).Single();
        var inventario = await Abrir(client, loja.Id);

        (await client.PostAsJsonAsync(U("/api/estoque/inventarios"), new { lojaId = loja.Id }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await client.PostAsync(U($"/api/estoque/inventarios/{inventario.Id}/cancelar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await Abrir(client, loja.Id);

        var lista = (await client.GetFromJsonAsync<List<InventarioDto>>(U("/api/estoque/inventarios"), Ct))!;
        lista.Select(i => i.Status).ShouldBe(["Aberto", "Cancelado"]);
    }

    [Fact]
    public async Task SemZerar_NaoContadosFicamComoEstao()
    {
        var client = await Entrar(_api);
        var camiseta = await CriarCamiseta(client);
        var loja = (await Lojas(client)).Single();
        var (azulM, pretoG) = (Sku(camiseta, "Azul", "M"), Sku(camiseta, "Preto", "G"));
        await Entrada(client, loja.Id, new { skuId = azulM.Id, quantidade = 5 }, new { skuId = pretoG.Id, quantidade = 3 });
        var inventario = await Abrir(client, loja.Id);
        await Contar(client, inventario.Id, new { skuId = azulM.Id, quantidade = 6 });

        await client.PostAsJsonAsync(U($"/api/estoque/inventarios/{inventario.Id}/concluir"), new { zerarNaoContados = false }, Ct);

        (await Saldo(client, camiseta.Id, azulM.Id)).ShouldBe(6);
        (await Saldo(client, camiseta.Id, pretoG.Id)).ShouldBe(3);
    }

    [Fact]
    public async Task Permissoes_EstoquistaContaMasSoDonoOuGerenteConclui_VendedorNada()
    {
        var admin = await Entrar(_api);
        var loja = (await Lojas(admin)).Single();
        var inventario = await Abrir(admin, loja.Id);
        var vendedor = await Entrar(_api, "vendedor@kamus.local");

        (await vendedor.GetAsync(U("/api/estoque/inventarios"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await vendedor.PostAsJsonAsync(U($"/api/estoque/inventarios/{inventario.Id}/concluir"), new { zerarNaoContados = false }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Relatorio_DivergenciasEmCsv()
    {
        var client = await Entrar(_api);
        var camiseta = await CriarCamiseta(client);
        var loja = (await Lojas(client)).Single();
        await Entrada(client, loja.Id, new { skuId = Sku(camiseta, "Azul", "M").Id, quantidade = 5 });
        var inventario = await Abrir(client, loja.Id);
        await Contar(client, inventario.Id, new { skuId = Sku(camiseta, "Azul", "M").Id, quantidade = 2 });

        var csv = await client.GetStringAsync(U($"/api/relatorios/inventario.csv?inventarioId={inventario.Id}"), Ct);

        csv.ShouldContain("CAM-001");
        csv.ShouldContain("-3");
    }
}
