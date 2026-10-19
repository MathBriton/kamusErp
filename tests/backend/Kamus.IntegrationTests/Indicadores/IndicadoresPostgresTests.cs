using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infra;
using Npgsql;
using static Kamus.IntegrationTests.Estoque.ApiDoEstoque;

namespace Kamus.IntegrationTests.Indicadores;

/// <summary>Indicadores no PostgreSQL: o dashboard consulta os três módulos e as preferências do resumo ficam no schema indicadores com RLS.</summary>
[Collection(PostgresGroup.Name)]
public sealed class IndicadoresPostgresTests(PostgresFixture db)
{
    private sealed record EmpresaDto(Guid Id, string NomeFantasia);
    private sealed record SessaoDto(string AccessToken, List<EmpresaDto> Empresas);
    private static readonly int[] DiasUteis = [1, 2, 3, 4, 5];

    [Fact]
    public async Task Indicadores_NoPostgres_DashboardPreferenciasERls()
    {
        db.ExigirBanco();
        await using var api = new KamusApiFactory(db.AppConnectionString, "Development", new Dictionary<string, string>
        {
            ["ConnectionStrings:KamusMigrations"] = db.AdminConnectionString,
            ["Seed:SenhaAdmin"] = Senha,
        });
        using var client = api.CreateClient();
        var login = await client.PostAsJsonAsync(U("/api/auth/login"), new { email = "admin@kamus.local", senha = Senha }, Ct);
        var sessao = (await login.Content.ReadFromJsonAsync<SessaoDto>(Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        client.DefaultRequestHeaders.Add("X-Empresa-Id", sessao.Empresas.Single(e => e.NomeFantasia == "Kamus Moda Centro").Id.ToString());

        var dashboard = await client.GetAsync(U("/api/indicadores?periodo=7dias"), Ct);
        dashboard.StatusCode.ShouldBe(HttpStatusCode.OK, await dashboard.Content.ReadAsStringAsync(Ct));
        var corpo = await dashboard.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct);
        corpo.GetProperty("vendas").GetProperty("ultimos30Dias").GetArrayLength().ShouldBe(30);
        corpo.GetProperty("estoque").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Object);

        var preferencias = await client.PutAsJsonAsync(U("/api/indicadores/resumo-diario"), new
        {
            ativo = true,
            hora = 7,
            dias = DiasUteis,
            vendas = true,
            financeiro = true,
            estoque = false,
            condicionais = true,
            crediario = true,
            enviarSemMovimento = false,
        }, Ct);
        preferencias.StatusCode.ShouldBe(HttpStatusCode.OK, await preferencias.Content.ReadAsStringAsync(Ct));
        (await client.GetFromJsonAsync<System.Text.Json.JsonElement>(U("/api/indicadores/resumo-diario"), Ct)).GetProperty("hora").GetInt32().ShouldBe(7);

        await using var conn = new NpgsqlConnection(db.AppConnectionString);
        await conn.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand("SELECT count(*) FROM indicadores.preferencias_do_resumo", conn);
        ((long)(await cmd.ExecuteScalarAsync(Ct))!).ShouldBe(0, "RLS em indicadores.preferencias_do_resumo");
    }
}
