using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infra;
using Kamus.Modules.Licenciamento.Aplicacao;
using Kamus.Modules.Licenciamento.Persistencia;
using Kamus.SharedKernel.Tempo;
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
    private sealed record TokenDto(string Token);
    private sealed record NovoClienteDto(Guid TenantId, string SenhaProvisoria);

    private KamusApiFactory CriarApi() => new(db.AppConnectionString, "Development", new Dictionary<string, string>
    {
        ["ConnectionStrings:KamusMigrations"] = db.AdminConnectionString,
        ["Seed:SenhaAdmin"] = Senha,
        ["Admin:Chave"] = "chave-do-painel-administrativo-de-teste-32+",
    });

    [Fact]
    public async Task Painel_NoPostgres_CriaClienteQueConsegueEntrarERegistraPagamento()
    {
        db.ExigirBanco();
        await using var api = CriarApi();
        using var admin = api.CreateClient();
        var login = await admin.PostAsJsonAsync(new Uri("/admin-api/login", UriKind.Relative), new { email = "superadmin@kamus.local", senha = KamusApiFactory.SenhaSuperAdminDeTeste }, Ct);
        login.EnsureSuccessStatusCode();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<TokenDto>(Ct))!.Token);

        var criado = await admin.PostAsJsonAsync(new Uri("/admin-api/clientes", UriKind.Relative), new
        {
            nome = "Bella Moda",
            razaoSocial = "Bella Moda LTDA",
            nomeFantasia = "Bella Moda",
            cnpj = "11.222.333/0001-81",
            nomeDoDono = "Bella",
            emailDoDono = "bella@bellamoda.com.br",
            valorMensal = 149.90m,
        }, Ct);
        criado.EnsureSuccessStatusCode();
        var novo = (await criado.Content.ReadFromJsonAsync<NovoClienteDto>(Ct))!;
        (await admin.PostAsJsonAsync(new Uri($"/admin-api/clientes/{novo.TenantId}/pagamentos", UriKind.Relative), new { valor = 149.90m, meses = 1 }, Ct))
            .EnsureSuccessStatusCode();

        using var cliente = api.CreateClient();
        var entrar = await cliente.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative), new { email = "bella@bellamoda.com.br", senha = novo.SenhaProvisoria }, Ct);
        entrar.EnsureSuccessStatusCode();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await entrar.Content.ReadFromJsonAsync<SessaoDto>(Ct))!.AccessToken);
        (await cliente.PostAsJsonAsync(new Uri("/api/auth/trocar-senha", UriKind.Relative), new { senhaAtual = novo.SenhaProvisoria, novaSenha = "BellaModa2026" }, Ct))
            .EnsureSuccessStatusCode();
        entrar = await cliente.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative), new { email = "bella@bellamoda.com.br", senha = "BellaModa2026" }, Ct);
        entrar.EnsureSuccessStatusCode();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await entrar.Content.ReadFromJsonAsync<SessaoDto>(Ct))!.AccessToken);
        (await cliente.GetFromJsonAsync<LicencaDto>(new Uri("/api/licenca", UriKind.Relative), Ct))!.Estado.ShouldBe("Ativa");
        (await cliente.GetFromJsonAsync<ListaDto>(new Uri("/api/notificacoes", UriKind.Relative), Ct))!.Itens.ShouldContain(n => n.Tipo == "licenca_paga");
    }

    [Fact]
    public async Task Licenca_NoPostgres_SituacaoEAvisoAosDonos()
    {
        db.ExigirBanco();
        await using var api = CriarApi();
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
