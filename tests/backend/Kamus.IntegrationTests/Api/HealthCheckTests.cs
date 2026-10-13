using System.Net;
using Kamus.IntegrationTests.Infra;

namespace Kamus.IntegrationTests.Api;

[Collection(PostgresGroup.Name)]
public sealed class HealthCheckTests(PostgresFixture db)
{
    [Fact]
    public async Task Health_ComBancoDisponivel_RetornaHealthy()
    {
        await using var factory = new KamusApiFactory(db.AdminConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Health_ComBancoIndisponivel_RetornaUnhealthy()
    {
        await using var factory = new KamusApiFactory("Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2");
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }
}
