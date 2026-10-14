using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infra;
using Npgsql;

namespace Kamus.IntegrationTests.Cadastros;

/// <summary>Clientes no PostgreSQL: RLS na tabela e CPF único por cliente do SaaS (índice parcial: vários sem documento).</summary>
[Collection(PostgresGroup.Name)]
public sealed class CadastrosPostgresTests(PostgresFixture db)
{
    private const string Senha = "Senha@Teste123";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record EmpresaDto(Guid Id);
    private sealed record SessaoDto(string AccessToken, List<EmpresaDto> Empresas);

    [Fact]
    public async Task Clientes_NoPostgres_DocumentoUnicoERls()
    {
        db.ExigirBanco();
        await using var api = new KamusApiFactory(db.AppConnectionString, "Development", new Dictionary<string, string>
        {
            ["ConnectionStrings:KamusMigrations"] = db.AdminConnectionString,
            ["Seed:SenhaAdmin"] = Senha,
        });
        using var client = api.CreateClient();
        var login = await client.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative), new { email = "admin@kamus.local", senha = Senha }, Ct);
        var sessao = (await login.Content.ReadFromJsonAsync<SessaoDto>(Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        client.DefaultRequestHeaders.Add("X-Empresa-Id", sessao.Empresas[0].Id.ToString());

        async Task<HttpStatusCode> Criar(string nome, string? cpf) =>
            (await client.PostAsJsonAsync(new Uri("/api/pessoas", UriKind.Relative), new { tipo = "Fisica", nome, documento = cpf, ehCliente = true }, Ct)).StatusCode;

        (await Criar("Sem CPF 1", null)).ShouldBe(HttpStatusCode.Created);
        (await Criar("Sem CPF 2", null)).ShouldBe(HttpStatusCode.Created);
        (await Criar("Com CPF", "111.444.777-35")).ShouldBe(HttpStatusCode.Created);
        (await Criar("CPF repetido", "111.444.777-35")).ShouldBe(HttpStatusCode.Conflict);

        await using var conn = new NpgsqlConnection(db.AppConnectionString);
        await conn.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand("SELECT count(*) FROM cadastros.pessoas", conn);
        ((long)(await cmd.ExecuteScalarAsync(Ct))!).ShouldBe(0); // sem app.tenant_id, o RLS não entrega nada
    }
}
