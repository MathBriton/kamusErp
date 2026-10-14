using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kamus.IntegrationTests.Infra;
using Kamus.Modules.Licenciamento.Aplicacao;
using Kamus.Modules.Licenciamento.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.IntegrationTests.Administracao;

/// <summary>Painel Administrativo (operador do SaaS): login próprio, clientes, pagamentos que liberam na hora.</summary>
public sealed class PainelAdministrativoTests : IAsyncDisposable
{
    private const string Senha = "Senha@Teste123";
    private const string SenhaSuperAdmin = "Super@Teste123";
    private const string EmailSuperAdmin = "superadmin@kamus.local";
    private static readonly Guid TenantDemo = Guid.Parse("d0000000-0000-0000-0000-000000000001");

    private readonly KamusApiFactory _factory;

    public PainelAdministrativoTests() : this(null) { }

    private PainelAdministrativoTests(IReadOnlyDictionary<string, string>? extras)
    {
        var configuracoes = new Dictionary<string, string>
        {
            ["Seed:SenhaAdmin"] = Senha,
            ["Seed:SenhaSuperAdmin"] = SenhaSuperAdmin,
            ["Admin:Chave"] = "chave-do-painel-administrativo-de-teste-32+",
        };
        foreach (var (k, v) in extras ?? new Dictionary<string, string>()) configuracoes[k] = v;
        _factory = new KamusApiFactory(ambiente: "Development", configuracoes: configuracoes);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Hoje => CalendarioBrasilia.Hoje(TimeProvider.System);
    private static Uri U(string caminho) => new(caminho, UriKind.Relative);

    private sealed record LoginAdminDto(string Token, string Nome);
    private sealed record ClienteDto(Guid TenantId, string Nome, string? EmailDoDono, int Usuarios, string Estado, DateOnly? Vencimento, decimal ValorMensal);
    private sealed record ResumoDto(int Ativas, int Trial, int EmCarencia, int Suspensas, int Canceladas, decimal ReceitaMensal);
    private sealed record AcaoDto(string Acao, string Descricao, string AdministradorEmail, Guid? TenantId);
    private sealed record NovoClienteDto(Guid TenantId, string SenhaProvisoria);
    private sealed record SessaoDto(string AccessToken, List<EmpresaDto> Empresas);
    private sealed record EmpresaDto(Guid Id);
    private sealed record LicencaDto(string Estado);
    private sealed record NotificacaoDto(string Tipo);
    private sealed record ListaDto(List<NotificacaoDto> Itens);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<HttpClient> EntrarComoAdmin()
    {
        var client = _factory.CreateClient();
        var resposta = await client.PostAsJsonAsync(U("/admin-api/login"), new { email = EmailSuperAdmin, senha = SenhaSuperAdmin }, Ct);
        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
        var login = (await resposta.Content.ReadFromJsonAsync<LoginAdminDto>(Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        return client;
    }

    private async Task<HttpClient> EntrarComoCliente(string email = "admin@kamus.local", string senha = Senha)
    {
        var client = _factory.CreateClient();
        var resposta = await client.PostAsJsonAsync(U("/api/auth/login"), new { email, senha }, Ct);
        resposta.EnsureSuccessStatusCode();
        var sessao = (await resposta.Content.ReadFromJsonAsync<SessaoDto>(Ct))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        client.DefaultRequestHeaders.Add("X-Empresa-Id", sessao.Empresas[0].Id.ToString());
        return client;
    }

    private async Task SuspenderDemo()
    {
        await using var escopo = _factory.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<LicenciamentoDbContext>();
        (await db.Licencas.SingleAsync(l => l.TenantId == TenantDemo, Ct)).RegistrarPagamento(Hoje.AddDays(-30));
        await db.SaveChangesAsync(Ct);
        escopo.ServiceProvider.GetRequiredService<CacheDeLicencas>().Invalidar(TenantDemo);
    }

    [Fact]
    public async Task Login_SenhaErrada_401()
    {
        using var client = _factory.CreateClient();

        var resposta = await client.PostAsJsonAsync(U("/admin-api/login"), new { email = EmailSuperAdmin, senha = "errada" }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_CincoFalhas_BloqueiaMesmoComSenhaCerta()
    {
        using var client = _factory.CreateClient();
        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync(U("/admin-api/login"), new { email = EmailSuperAdmin, senha = "errada" }, Ct);

        var resposta = await client.PostAsJsonAsync(U("/admin-api/login"), new { email = EmailSuperAdmin, senha = SenhaSuperAdmin }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await resposta.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("detail").GetString()!.ShouldContain("bloqueado");
    }

    [Fact]
    public async Task Tokens_ClienteNaoEntraNoPainelEAdminNaoEntraNoCliente()
    {
        var cliente = await EntrarComoCliente();
        var admin = await EntrarComoAdmin();

        (await cliente.GetAsync(U("/admin-api/clientes"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await admin.GetAsync(U("/api/licenca"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var anonimo = _factory.CreateClient();
        (await anonimo.GetAsync(U("/admin-api/clientes"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Clientes_ListaComSituacaoDaLicencaEDono()
    {
        var admin = await EntrarComoAdmin();

        var clientes = await admin.GetFromJsonAsync<List<ClienteDto>>(U("/admin-api/clientes"), Ct);

        var demo = clientes.ShouldNotBeNull().ShouldHaveSingleItem();
        demo.Nome.ShouldBe("Loja Demo");
        demo.EmailDoDono.ShouldBe("admin@kamus.local");
        demo.Usuarios.ShouldBe(2);
        demo.Estado.ShouldBe("Ativa");
        demo.ValorMensal.ShouldBe(199.90m);
    }

    [Fact]
    public async Task Resumo_ContaPorSituacaoEReceita()
    {
        var admin = await EntrarComoAdmin();

        var resumo = (await admin.GetFromJsonAsync<ResumoDto>(U("/admin-api/resumo"), Ct))!;

        resumo.Ativas.ShouldBe(1);
        resumo.Suspensas.ShouldBe(0);
        resumo.ReceitaMensal.ShouldBe(199.90m);
    }

    [Fact]
    public async Task RegistrarPagamento_LiberaNaHoraNotificaERegistraAcao()
    {
        var cliente = await EntrarComoCliente();
        var admin = await EntrarComoAdmin();
        await SuspenderDemo();
        (await cliente.GetAsync(U("/api/auditoria"), Ct)).StatusCode.ShouldBe(HttpStatusCode.PaymentRequired);

        var resposta = await admin.PostAsJsonAsync(U($"/admin-api/clientes/{TenantDemo}/pagamentos"),
            new { valor = 199.90m, meses = 1, forma = "Pix", observacao = "Comprovante por WhatsApp" }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await cliente.GetAsync(U("/api/auditoria"), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await cliente.GetFromJsonAsync<LicencaDto>(U("/api/licenca"), Ct))!.Estado.ShouldBe("Ativa");
        (await cliente.GetFromJsonAsync<ListaDto>(U("/api/notificacoes"), Ct))!.Itens.ShouldContain(n => n.Tipo == "licenca_paga");
        var acoes = await admin.GetFromJsonAsync<List<AcaoDto>>(U("/admin-api/acoes"), Ct);
        acoes.ShouldNotBeNull().ShouldContain(a => a.Acao == "pagamento_registrado" && a.TenantId == TenantDemo && a.AdministradorEmail == EmailSuperAdmin);
    }

    [Fact]
    public async Task RegistrarPagamento_ValorInvalido_400()
    {
        var admin = await EntrarComoAdmin();

        var resposta = await admin.PostAsJsonAsync(U($"/admin-api/clientes/{TenantDemo}/pagamentos"), new { valor = 0m, meses = 1 }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CancelarEReativar_BloqueiaELibera()
    {
        var cliente = await EntrarComoCliente();
        var admin = await EntrarComoAdmin();

        (await admin.PostAsync(U($"/admin-api/clientes/{TenantDemo}/cancelar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var problema = await (await cliente.GetAsync(U("/api/auditoria"), Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);
        problema.GetProperty("codigo").GetString().ShouldBe("LICENCA_CANCELADA");

        (await admin.PostAsync(U($"/admin-api/clientes/{TenantDemo}/reativar"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await cliente.GetAsync(U("/api/auditoria"), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AlterarLicenca_ValorELimites()
    {
        var admin = await EntrarComoAdmin();

        var resposta = await admin.PutAsJsonAsync(U($"/admin-api/clientes/{TenantDemo}/licenca"),
            new { valorMensal = 249.90m, limiteDeCnpjs = 2, limiteDeLojas = (int?)null, limiteDeUsuarios = 5 }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var cliente = await EntrarComoCliente();
        var licenca = await cliente.GetFromJsonAsync<JsonElement>(U("/api/licenca"), Ct);
        licenca.GetProperty("valorMensal").GetDecimal().ShouldBe(249.90m);
        licenca.GetProperty("limiteDeUsuarios").GetInt32().ShouldBe(5);
    }

    [Fact]
    public async Task NovoCliente_CriaDonoEmpresaELicencaEmAvaliacao()
    {
        var admin = await EntrarComoAdmin();

        var resposta = await admin.PostAsJsonAsync(U("/admin-api/clientes"), new
        {
            nome = "Ateliê Flor de Lis",
            razaoSocial = "Flor de Lis Moda LTDA",
            nomeFantasia = "Ateliê Flor de Lis",
            cnpj = "11.222.333/0001-81",
            nomeDoDono = "Ana Flor",
            emailDoDono = "ana@flordelis.com.br",
            valorMensal = 149.90m,
        }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Created);
        var criado = (await resposta.Content.ReadFromJsonAsync<NovoClienteDto>(Ct))!;
        criado.SenhaProvisoria.Length.ShouldBeGreaterThanOrEqualTo(10);

        var cliente = await EntrarComoCliente("ana@flordelis.com.br", criado.SenhaProvisoria);
        (await cliente.GetFromJsonAsync<LicencaDto>(U("/api/licenca"), Ct))!.Estado.ShouldBe("Trial");
        var clientes = await admin.GetFromJsonAsync<List<ClienteDto>>(U("/admin-api/clientes"), Ct);
        clientes.ShouldNotBeNull().ShouldContain(c => c.TenantId == criado.TenantId && c.Estado == "Trial" && c.ValorMensal == 149.90m);
    }

    [Fact]
    public async Task NovoCliente_EmailJaUsado_409()
    {
        var admin = await EntrarComoAdmin();

        var resposta = await admin.PostAsJsonAsync(U("/admin-api/clientes"), new
        {
            nome = "Outra",
            razaoSocial = "Outra LTDA",
            nomeFantasia = "Outra",
            cnpj = "11.222.333/0001-81",
            nomeDoDono = "Fulano",
            emailDoDono = "admin@kamus.local",
            valorMensal = 99m,
        }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task NovoCliente_CnpjInvalido_400()
    {
        var admin = await EntrarComoAdmin();

        var resposta = await admin.PostAsJsonAsync(U("/admin-api/clientes"), new
        {
            nome = "Outra",
            razaoSocial = "Outra LTDA",
            nomeFantasia = "Outra",
            cnpj = "11.111.111/1111-11",
            nomeDoDono = "Fulano",
            emailDoDono = "fulano@outra.com",
            valorMensal = 99m,
        }, Ct);

        resposta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SemChaveConfigurada_PainelNaoExiste()
    {
        await using var semPainel = new KamusApiFactory(ambiente: "Development", configuracoes: new Dictionary<string, string>
        {
            ["Seed:SenhaAdmin"] = Senha,
            ["Admin:Chave"] = "",
        });
        using var client = semPainel.CreateClient();

        (await client.PostAsJsonAsync(U("/admin-api/login"), new { email = EmailSuperAdmin, senha = SenhaSuperAdmin }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task IpForaDaListaPermitida_404()
    {
        await using var restrito = new PainelAdministrativoTests(new Dictionary<string, string> { ["Admin:IpsPermitidos"] = "10.9.9.9" });
        using var client = restrito._factory.CreateClient();

        (await client.PostAsJsonAsync(U("/admin-api/login"), new { email = EmailSuperAdmin, senha = SenhaSuperAdmin }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
