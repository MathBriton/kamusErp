using System.Net;
using System.Net.Http.Json;
using System.Text;
using static Kamus.IntegrationTests.Estoque.ApiDoEstoque;

namespace Kamus.IntegrationTests.Estoque;

/// <summary>Etiquetas em PDF: escolhendo SKUs e quantidades, ou as peças recebidas numa entrada.</summary>
public sealed class EtiquetasTests : IAsyncDisposable
{
    private readonly Infra.KamusApiFactory _api = NovaApi();

    public ValueTask DisposeAsync() => _api.DisposeAsync();

    private static int Paginas(byte[] pdf) => System.Text.RegularExpressions.Regex.Count(Encoding.Latin1.GetString(pdf), @"/Type\s*/Page[^s]");

    [Fact]
    public async Task PorItens_TermicaUmaPorPeca()
    {
        var client = await Entrar(_api);
        var camiseta = await CriarCamiseta(client);

        var resposta = await client.PostAsJsonAsync(U("/api/estoque/etiquetas"), new
        {
            formato = "termica",
            itens = new[] { new { skuId = Sku(camiseta, "Azul", "M").Id, quantidade = 2 }, new { skuId = Sku(camiseta, "Preto", "G").Id, quantidade = 1 } },
        }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
        resposta.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        resposta.Content.Headers.ContentDisposition!.FileNameStar.ShouldBe("etiquetas.pdf");
        Paginas(await resposta.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(3);
    }

    [Fact]
    public async Task PorItens_Invalidos_400()
    {
        var client = await Entrar(_api);
        var camiseta = await CriarCamiseta(client);
        var azulM = Sku(camiseta, "Azul", "M").Id;

        (await client.PostAsJsonAsync(U("/api/estoque/etiquetas"), new { itens = Array.Empty<object>() }, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(U("/api/estoque/etiquetas"), new { formato = "zebra", itens = new[] { new { skuId = azulM, quantidade = 1 } } }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(U("/api/estoque/etiquetas"), new { itens = new[] { new { skuId = azulM, quantidade = 5000 } } }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(U("/api/estoque/etiquetas"), new { itens = new[] { new { skuId = Guid.NewGuid(), quantidade = 1 } } }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DaEntrada_QuantidadeRecebida()
    {
        var client = await Entrar(_api);
        var camiseta = await CriarCamiseta(client);
        var loja = (await Lojas(client)).Single();
        var entrada = await Entrada(client, loja.Id, new { skuId = Sku(camiseta, "Azul", "M").Id, quantidade = 4 }, new { skuId = Sku(camiseta, "Preto", "P").Id, quantidade = 2 });

        var resposta = await client.GetAsync(U($"/api/estoque/etiquetas/operacao/{entrada.OperacaoId}?formato=termica"), Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
        Paginas(await resposta.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(6);
        (await client.GetAsync(U($"/api/estoque/etiquetas/operacao/{Guid.NewGuid()}"), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Vendedor_NaoGeraEtiquetas()
    {
        var admin = await Entrar(_api);
        var camiseta = await CriarCamiseta(admin);
        var vendedor = await Entrar(_api, "vendedor@kamus.local");

        (await vendedor.PostAsJsonAsync(U("/api/estoque/etiquetas"), new { itens = new[] { new { skuId = Sku(camiseta, "Azul", "M").Id, quantidade = 1 } } }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
