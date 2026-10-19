using System.Net;
using System.Net.Http.Json;
using Kamus.IntegrationTests.Infra;

namespace Kamus.IntegrationTests.Identidade;

/// <summary>Login do app mobile: o token de sessão vai no corpo (o app guarda no armazenamento seguro), sem cookie.</summary>
public sealed class AutenticacaoMobileTests : IAsyncDisposable
{
    private const string Senha = "Senha@Teste123";

    private readonly KamusApiFactory _factory = new(ambiente: "Development", configuracoes: new Dictionary<string, string> { ["Seed:SenhaAdmin"] = Senha });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Uri U(string caminho) => new(caminho, UriKind.Relative);

    private sealed record UsuarioDto(Guid Id, string Nome, string Email);
    private sealed record EmpresaDto(Guid Id, string NomeFantasia, string Perfil);
    private sealed record SessaoMobileDto(string AccessToken, DateTimeOffset ExpiraEm, UsuarioDto Usuario, List<EmpresaDto> Empresas, string TokenDeSessao, DateTimeOffset SessaoExpiraEm);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static async Task<SessaoMobileDto> Entrar(HttpClient client)
    {
        var r = await client.PostAsJsonAsync(U("/api/auth/mobile/login"), new { email = "admin@kamus.local", senha = Senha }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.OK, await r.Content.ReadAsStringAsync(Ct));
        r.Headers.Contains("Set-Cookie").ShouldBeFalse();
        return (await r.Content.ReadFromJsonAsync<SessaoMobileDto>(Ct))!;
    }

    [Fact]
    public async Task LoginMobile_DevolveTokenDeSessaoNoCorpoESemCookie()
    {
        using var client = _factory.CreateClient();

        var sessao = await Entrar(client);

        sessao.AccessToken.ShouldNotBeNullOrWhiteSpace();
        sessao.TokenDeSessao.ShouldNotBeNullOrWhiteSpace();
        sessao.Usuario.Email.ShouldBe("admin@kamus.local");
        sessao.Empresas.Count.ShouldBe(2);
        sessao.SessaoExpiraEm.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddDays(1));
    }

    [Fact]
    public async Task LoginMobile_SenhaErrada_401()
    {
        using var client = _factory.CreateClient();

        var r = await client.PostAsJsonAsync(U("/api/auth/mobile/login"), new { email = "admin@kamus.local", senha = "errada" }, Ct);

        r.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RenovarMobile_TrocaOTokenDeSessaoEOAntigoNaoValeMais()
    {
        using var client = _factory.CreateClient();
        var sessao = await Entrar(client);

        var r = await client.PostAsJsonAsync(U("/api/auth/mobile/refresh"), new { sessao.TokenDeSessao }, Ct);

        r.StatusCode.ShouldBe(HttpStatusCode.OK);
        var nova = (await r.Content.ReadFromJsonAsync<SessaoMobileDto>(Ct))!;
        nova.TokenDeSessao.ShouldNotBe(sessao.TokenDeSessao);
        nova.AccessToken.ShouldNotBeNullOrWhiteSpace();
        (await client.PostAsJsonAsync(U("/api/auth/mobile/refresh"), new { tokenDeSessao = "invalido" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SairMobile_EncerraASessao()
    {
        using var client = _factory.CreateClient();
        var sessao = await Entrar(client);

        (await client.PostAsJsonAsync(U("/api/auth/mobile/logout"), new { sessao.TokenDeSessao }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.PostAsJsonAsync(U("/api/auth/mobile/refresh"), new { sessao.TokenDeSessao }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Cors_EmDesenvolvimento_LiberaOExpoWebENaoOutrasOrigens()
    {
        using var client = _factory.CreateClient();

        HttpRequestMessage Preflight(string origem)
        {
            var m = new HttpRequestMessage(HttpMethod.Options, U("/api/indicadores"));
            m.Headers.Add("Origin", origem);
            m.Headers.Add("Access-Control-Request-Method", "GET");
            m.Headers.Add("Access-Control-Request-Headers", "authorization,x-empresa-id");
            return m;
        }

        using var liberada = await client.SendAsync(Preflight("http://localhost:8081"), Ct);
        liberada.Headers.GetValues("Access-Control-Allow-Origin").Single().ShouldBe("http://localhost:8081");
        using var outra = await client.SendAsync(Preflight("http://malicioso.example"), Ct);
        outra.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }
}
