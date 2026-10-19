using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infra;
using Npgsql;

namespace Kamus.IntegrationTests.Notificacoes;

/// <summary>App mobile no PostgreSQL: login com token de sessão no corpo, aparelho registrado e RLS na tabela de aparelhos.</summary>
[Collection(PostgresGroup.Name)]
public sealed class PushPostgresTests(PostgresFixture db)
{
    private const string Senha = "Senha@Teste123";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Uri U(string caminho) => new(caminho, UriKind.Relative);

    private sealed record SessaoMobileDto(string AccessToken, string TokenDeSessao);

    [Fact]
    public async Task AppMobile_NoPostgres_LoginRenovacaoAparelhoERls()
    {
        db.ExigirBanco();
        await using var api = new KamusApiFactory(db.AppConnectionString, "Development", new Dictionary<string, string>
        {
            ["ConnectionStrings:KamusMigrations"] = db.AdminConnectionString,
            ["Seed:SenhaAdmin"] = Senha,
        });
        using var client = api.CreateClient();
        var login = await client.PostAsJsonAsync(U("/api/auth/mobile/login"), new { email = "admin@kamus.local", senha = Senha }, Ct);
        login.StatusCode.ShouldBe(HttpStatusCode.OK, await login.Content.ReadAsStringAsync(Ct));
        var sessao = (await login.Content.ReadFromJsonAsync<SessaoMobileDto>(Ct))!;
        var renovada = await client.PostAsJsonAsync(U("/api/auth/mobile/refresh"), new { sessao.TokenDeSessao }, Ct);
        renovada.StatusCode.ShouldBe(HttpStatusCode.OK);
        sessao = (await renovada.Content.ReadFromJsonAsync<SessaoMobileDto>(Ct))!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        var token = $"ExponentPushToken[{Guid.NewGuid():N}]";
        (await client.PutAsJsonAsync(U("/api/notificacoes/dispositivos"), new { token, plataforma = "ios" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PutAsJsonAsync(U("/api/notificacoes/dispositivos"), new { token, plataforma = "ios" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var conn = new NpgsqlConnection(db.AppConnectionString);
        await conn.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand("SELECT count(*) FROM notificacoes.dispositivos", conn);
        ((long)(await cmd.ExecuteScalarAsync(Ct))!).ShouldBe(0, "RLS em notificacoes.dispositivos");
    }
}
