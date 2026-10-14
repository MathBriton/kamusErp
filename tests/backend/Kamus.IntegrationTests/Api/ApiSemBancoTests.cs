using System.Net;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infra;

namespace Kamus.IntegrationTests.Api;

/// <summary>A API precisa subir e responder em localhost mesmo sem PostgreSQL nem Docker.</summary>
public sealed class ApiSemBancoTests
{
    private sealed record VersaoResponse(string Versao, string? Commit);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Health_SemBancoConfigurado_ApiSobeEInformaDegraded()
    {
        await using var factory = new KamusApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldBe("Degraded");
    }

    [Fact]
    public async Task Versao_SemBanco_RetornaVersaoDoAssembly()
    {
        await using var factory = new KamusApiFactory();
        using var client = factory.CreateClient();

        var corpo = await client.GetFromJsonAsync<VersaoResponse>(new Uri("/api/versao", UriKind.Relative), Ct);

        corpo.ShouldNotBeNull();
        corpo.Versao.ShouldMatch(@"^\d+\.\d+\.\d+");
    }

    [Fact]
    public async Task OpenApi_EmDevelopment_DocumentaOsEndpoints()
    {
        await using var factory = new KamusApiFactory(ambiente: "Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("/api/versao");
    }

    [Fact]
    public async Task TelaDeTesteScalar_EmDevelopment_Disponivel()
    {
        await using var factory = new KamusApiFactory(ambiente: "Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/scalar", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    public async Task DocumentacaoDaApi_ForaDeDevelopment_NaoEhExposta(string rota)
    {
        await using var factory = new KamusApiFactory(ambiente: "Production");
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri(rota, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
