using System.Net;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infra;
using Npgsql;

namespace Kamus.IntegrationTests.Identidade;

/// <summary>
/// Mesmo fluxo de login, agora no PostgreSQL: migration aplicada com o usuário administrador,
/// aplicação conectada como <c>kamus_app</c> (sujeita ao RLS). Pulado sem Docker local; obrigatório no CI.
/// </summary>
[Collection(PostgresGroup.Name)]
public sealed class AutenticacaoPostgresTests(PostgresFixture db)
{
    private const string SenhaDemo = "Senha@Teste123";
    private static readonly Guid TenantDemo = Guid.Parse("d0000000-0000-0000-0000-000000000001");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record EmpresaDto(string Cnpj);
    private sealed record LoginResponse(string AccessToken, List<EmpresaDto> Empresas);
    private sealed record NotificacaoDto(string Titulo);
    private sealed record ListaNotificacoes(List<NotificacaoDto> Itens, int NaoLidas);

    private KamusApiFactory CriarApi() => new(db.AppConnectionString, "Development", new Dictionary<string, string>
    {
        ["ConnectionStrings:KamusMigrations"] = db.AdminConnectionString,
        ["Seed:SenhaAdmin"] = SenhaDemo,
    });

    [Fact]
    public async Task Login_NoPostgresComRls_FuncionaComUsuarioDeTeste()
    {
        db.ExigirBanco();
        await using var api = CriarApi();
        using var client = api.CreateClient();

        var response = await client.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative),
            new { email = "admin@kamus.local", senha = SenhaDemo }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<LoginResponse>(Ct);
        corpo!.Empresas.Count.ShouldBe(2);

        // Notificações (outro módulo, outro schema) também funcionam no PostgreSQL com RLS
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", corpo.AccessToken);
        var notificacoes = await client.GetFromJsonAsync<ListaNotificacoes>(new Uri("/api/notificacoes", UriKind.Relative), Ct);
        notificacoes!.Itens.ShouldContain(n => n.Titulo == "Bem-vindo ao Kamus");
    }

    [Fact]
    public async Task Empresas_SemTenantNaSessao_BancoNaoEntregaNenhuma()
    {
        db.ExigirBanco();
        await using (var api = CriarApi())
            api.CreateClient().Dispose(); // sobe a API: aplica migration e cria os dados de demonstração

        await using var conn = new NpgsqlConnection(db.AppConnectionString);
        await conn.OpenAsync(Ct);

        (await ContarEmpresasAsync(conn)).ShouldBe(0);

        await using (var cmd = new NpgsqlCommand($"SELECT set_config('app.tenant_id', '{TenantDemo}', false)", conn))
            await cmd.ExecuteNonQueryAsync(Ct);

        (await ContarEmpresasAsync(conn)).ShouldBe(2);
    }

    [Fact]
    public async Task HistoricoNoPostgres_EhGravadoEProtegidoContraAlteracao()
    {
        db.ExigirBanco();
        await using (var api = CriarApi())
        using (var client = api.CreateClient())
            (await client.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative), new { email = "admin@kamus.local", senha = SenhaDemo }, Ct))
                .StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var conn = new NpgsqlConnection(db.AppConnectionString);
        await conn.OpenAsync(Ct);
        await using (var cmd = new NpgsqlCommand($"SELECT set_config('app.tenant_id', '{TenantDemo}', false)", conn))
            await cmd.ExecuteNonQueryAsync(Ct);

        await using (var contar = new NpgsqlCommand("SELECT count(*) FROM auditoria.registros WHERE evento = 'login'", conn))
            ((long)(await contar.ExecuteScalarAsync(Ct))!).ShouldBeGreaterThan(0);

        await using var apagar = new NpgsqlCommand("DELETE FROM auditoria.registros", conn);
        var erro = await Should.ThrowAsync<PostgresException>(() => apagar.ExecuteNonQueryAsync(Ct));
        erro.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    private static async Task<long> ContarEmpresasAsync(NpgsqlConnection conn)
    {
        await using var cmd = new NpgsqlCommand("SELECT count(*) FROM identidade.empresas", conn);
        return (long)(await cmd.ExecuteScalarAsync(Ct))!;
    }
}
