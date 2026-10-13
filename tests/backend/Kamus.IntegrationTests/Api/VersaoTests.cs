using System.Net;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infra;

namespace Kamus.IntegrationTests.Api;

[Collection(PostgresGroup.Name)]
public sealed class VersaoTests(PostgresFixture db)
{
    private sealed record VersaoResponse(string Versao, string? Commit);

    [Fact]
    public async Task Versao_RetornaVersaoDoAssemblySemDependerDoBanco()
    {
        await using var factory = new KamusApiFactory(db.AdminConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/versao", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<VersaoResponse>(TestContext.Current.CancellationToken);
        corpo.ShouldNotBeNull();
        corpo.Versao.ShouldMatch(@"^\d+\.\d+\.\d+");
    }
}
