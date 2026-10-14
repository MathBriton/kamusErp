using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infra;

namespace Kamus.IntegrationTests.Identidade;

/// <summary>
/// Login completo pela API com os dados de demonstração (Development), em memória — sem PostgreSQL nem Docker.
/// </summary>
public sealed class AutenticacaoTests : IAsyncDisposable
{
    private const string EmailDemo = "admin@kamus.local";
    private const string SenhaDemo = "Senha@Teste123";

    private readonly KamusApiFactory _factory = new(ambiente: "Development",
        configuracoes: new Dictionary<string, string> { ["Seed:SenhaAdmin"] = SenhaDemo });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record EmpresaDto(Guid Id, string NomeFantasia, string RazaoSocial, string Cnpj, string Perfil);
    private sealed record UsuarioDto(Guid Id, string Nome, string Email);
    private sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiraEm, UsuarioDto Usuario, List<EmpresaDto> Empresas);
    private sealed record MeResponse(UsuarioDto Usuario, List<EmpresaDto> Empresas);
    private sealed record Problema(string? Title, string? Detail);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static Task<HttpResponseMessage> Login(HttpClient client, string email, string senha) =>
        client.PostAsJsonAsync(new Uri("/api/auth/login", UriKind.Relative), new { email, senha }, Ct);

    [Fact]
    public async Task Login_ComUsuarioDeTeste_RetornaTokenUsuarioEEmpresas()
    {
        using var client = _factory.CreateClient();

        var response = await Login(client, EmailDemo, SenhaDemo);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<LoginResponse>(Ct);
        corpo!.AccessToken.ShouldNotBeNullOrWhiteSpace();
        corpo.Usuario.Nome.ShouldBe("Admin Demo");
        corpo.Usuario.Email.ShouldBe(EmailDemo);
        corpo.Empresas.Select(e => e.Cnpj).ShouldBe(["12.345.678/0001-95", "98.765.432/0001-98"], ignoreOrder: true);
        corpo.Empresas.ShouldAllBe(e => e.Perfil == "Dono");

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("kamus_refresh=", StringComparison.Ordinal));
        cookie.ShouldContain("httponly", Case.Insensitive);
        cookie.ShouldContain("path=/api/auth", Case.Insensitive);
    }

    [Fact]
    public async Task Login_EmailComMaiusculas_Funciona()
    {
        using var client = _factory.CreateClient();

        (await Login(client, "  ADMIN@Kamus.Local ", SenhaDemo)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(EmailDemo, "senha-errada")]
    [InlineData("ninguem@kamus.local", SenhaDemo)]
    public async Task Login_CredenciaisInvalidas_Retorna401ComMensagemGenerica(string email, string senha)
    {
        using var client = _factory.CreateClient();

        var response = await Login(client, email, senha);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.Content.ReadFromJsonAsync<Problema>(Ct))!.Detail.ShouldBe("E-mail ou senha inválidos.");
    }

    [Fact]
    public async Task Login_AposCincoSenhasErradas_BloqueiaMesmoComSenhaCorreta()
    {
        using var client = _factory.CreateClient();
        for (var i = 0; i < 5; i++) await Login(client, EmailDemo, "errada");

        var response = await Login(client, EmailDemo, SenhaDemo);

        response.StatusCode.ShouldBe(HttpStatusCode.Locked);
        (await response.Content.ReadFromJsonAsync<Problema>(Ct))!.Detail!.ShouldContain("bloqueado");
    }

    [Fact]
    public async Task Login_MuitasTentativasSeguidas_LimitaRequisicoes()
    {
        using var client = _factory.CreateClient();
        var codigos = new List<HttpStatusCode>();
        for (var i = 0; i < 12; i++) codigos.Add((await Login(client, "x@kamus.local", "x")).StatusCode);

        codigos.ShouldContain(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Me_ComToken_RetornaUsuarioEEmpresas()
    {
        using var client = _factory.CreateClient();
        var login = await (await Login(client, EmailDemo, SenhaDemo)).Content.ReadFromJsonAsync<LoginResponse>(Ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var me = await client.GetFromJsonAsync<MeResponse>(new Uri("/api/auth/me", UriKind.Relative), Ct);

        me!.Usuario.Email.ShouldBe(EmailDemo);
        me.Empresas.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Me_SemToken_Retorna401()
    {
        using var client = _factory.CreateClient();

        (await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ComCookie_EmiteNovoTokenERotacionaCookie()
    {
        using var client = _factory.CreateClient();
        await Login(client, EmailDemo, SenhaDemo);

        var response = await client.PostAsync(new Uri("/api/auth/refresh", UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<LoginResponse>(Ct))!.AccessToken.ShouldNotBeNullOrWhiteSpace();
        response.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith("kamus_refresh=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Refresh_ReusoDeTokenJaUsado_EhRejeitadoERevogaASessao()
    {
        using var client = _factory.CreateClient(new() { HandleCookies = false });
        var login = await Login(client, EmailDemo, SenhaDemo);
        var cookieOriginal = ExtrairCookie(login);

        var primeiro = await Refresh(client, cookieOriginal);
        primeiro.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cookieNovo = ExtrairCookie(primeiro);

        (await Refresh(client, cookieOriginal)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized); // reuso
        (await Refresh(client, cookieNovo)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);     // sessão revogada
    }

    [Fact]
    public async Task Refresh_SemCookie_Retorna401()
    {
        using var client = _factory.CreateClient();

        (await client.PostAsync(new Uri("/api/auth/refresh", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevogaASessao()
    {
        using var client = _factory.CreateClient();
        await Login(client, EmailDemo, SenhaDemo);

        (await client.PostAsync(new Uri("/api/auth/logout", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.PostAsync(new Uri("/api/auth/refresh", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DadosDeDemonstracao_ForaDeDevelopment_NaoSaoCriados()
    {
        await using var producao = new KamusApiFactory(ambiente: "Production",
            configuracoes: new Dictionary<string, string> { ["Seed:SenhaAdmin"] = SenhaDemo });
        using var client = producao.CreateClient();

        (await Login(client, EmailDemo, SenhaDemo)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static string ExtrairCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("kamus_refresh=", StringComparison.Ordinal)).Split(';')[0];

    private static Task<HttpResponseMessage> Refresh(HttpClient client, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/auth/refresh", UriKind.Relative));
        request.Headers.Add("Cookie", cookie);
        return client.SendAsync(request, Ct);
    }
}
