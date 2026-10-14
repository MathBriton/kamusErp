using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infra;
using Kamus.Modules.Licenciamento.Aplicacao;
using Kamus.Modules.Licenciamento.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Licenciamento;

/// <summary>Licença no PostgreSQL (aplicação como <c>kamus_app</c>): migration, seed e avisos que cruzam módulos com RLS.</summary>
[Collection(PostgresGroup.Name)]
public sealed class LicencaPostgresTests(PostgresFixture db)
{
    private const string Senha = "Senha@Teste123";
    private static readonly Guid TenantDemo = Guid.Parse("d0000000-0000-0000-0000-000000000001");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record SessaoDto(string AccessToken);
    private sealed record LicencaDto(string Estado);
    private sealed record NotificacaoDto(string Tipo);
    private sealed record ListaDto(List<NotificacaoDto> Itens);

    [Fact]
    public async Task Licenca_NoPostgres_SituacaoEAvisoAosDonos()
    {
        db.ExigirBanco();
        await using var api = new KamusApiFactory(db.AppConnectionString, "Development", new Dictionary<string, string>
        {
            ["ConnectionStrings:KamusMigrations"] = db.AdminConnectionString,
            ["Seed:SenhaAdmin"] = Senha,
        });
        using var client = api.CreateClient();
        var login = await client.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative), new { email = "admin@kamus.local", senha = Senha }, Ct);
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<SessaoDto>(Ct))!.AccessToken);

        (await client.GetFromJsonAsync<LicencaDto>(new Uri("/api/licenca", UriKind.Relative), Ct))!.Estado.ShouldBe("Ativa");

        var hoje = CalendarioBrasilia.Hoje(TimeProvider.System);
        await using (var escopo = api.Services.CreateAsyncScope())
        {
            var licencas = escopo.ServiceProvider.GetRequiredService<LicenciamentoDbContext>();
            (await licencas.Licencas.SingleAsync(l => l.TenantId == TenantDemo, Ct)).RegistrarPagamento(hoje.AddDays(7));
            await licencas.SaveChangesAsync(Ct);
            await escopo.ServiceProvider.GetRequiredService<VerificadorDeVencimentos>().ExecutarAsync(hoje, Ct);
        }

        (await client.GetFromJsonAsync<ListaDto>(new Uri("/api/notificacoes", UriKind.Relative), Ct))!.Itens
            .ShouldContain(n => n.Tipo == "licenca_vencendo");
    }
}
