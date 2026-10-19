using System.Security.Claims;
using Kamus.Modules.Identidade.Aplicacao;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Kamus.Modules.Identidade.Endpoints;

internal static class AutenticacaoEndpoints
{
    public const string CookieDeSessao = "kamus_refresh";
    public const string PoliticaDeLimiteDoLogin = "login";
    private const string CaminhoDoCookie = "/api/auth";

    public static IEndpointRouteBuilder MapAutenticacao(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/auth").WithTags("Autenticação");

        grupo.MapPost("/login", Login)
            .AllowAnonymous()
            .RequireRateLimiting(PoliticaDeLimiteDoLogin)
            .WithSummary("Entrar com e-mail e senha")
            .Produces<SessaoResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status423Locked)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        grupo.MapPost("/refresh", Renovar)
            .AllowAnonymous()
            .WithSummary("Renovar o token de acesso usando o cookie de sessão")
            .Produces<SessaoResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        grupo.MapPost("/logout", Sair)
            .AllowAnonymous()
            .WithSummary("Encerrar a sessão")
            .Produces(StatusCodes.Status204NoContent);

        grupo.MapPost("/trocar-senha", TrocarSenha)
            .RequireAuthorization()
            .WithSummary("Trocar a própria senha (obrigatório quando a senha é provisória). Depois, renove a sessão em /refresh")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        grupo.MapGet("/me", UsuarioAtual)
            .RequireAuthorization()
            .WithSummary("Usuário logado e empresas que ele acessa")
            .Produces<MeResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        var mobile = grupo.MapGroup("/mobile");
        mobile.MapPost("/login", LoginMobile)
            .AllowAnonymous()
            .RequireRateLimiting(PoliticaDeLimiteDoLogin)
            .WithSummary("[App mobile] Entrar: devolve o token de sessão no corpo (sem cookie), sempre \"manter conectado\"")
            .Produces<SessaoMobileResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status423Locked);
        mobile.MapPost("/refresh", RenovarMobile)
            .AllowAnonymous()
            .WithSummary("[App mobile] Renovar com o token de sessão (que é trocado a cada uso)")
            .Produces<SessaoMobileResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        mobile.MapPost("/logout", SairMobile)
            .AllowAnonymous()
            .WithSummary("[App mobile] Encerrar a sessão do aparelho")
            .Produces(StatusCodes.Status204NoContent);

        return app;
    }

    private static SessaoMobileResponse Mobile(SessaoEmitida s) =>
        new(s.Resposta.AccessToken, s.Resposta.ExpiraEm, s.Resposta.Usuario, s.Resposta.Empresas, s.TokenDeSessao, s.SessaoExpiraEm);

    private static async Task<IResult> LoginMobile(LoginRequest request, AutenticacaoService auth, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Senha))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Dados incompletos", detail: "Informe e-mail e senha.");

        return await auth.LoginAsync(request with { ManterConectado = true }, ct) switch
        {
            ResultadoLogin.Sucesso s => Results.Ok(Mobile(s.Sessao)),
            ResultadoLogin.Bloqueado b => UsuarioBloqueado(b.Ate),
            _ => CredenciaisInvalidas(),
        };
    }

    private static async Task<IResult> RenovarMobile(TokenDeSessaoRequest request, AutenticacaoService auth, CancellationToken ct) =>
        await auth.RenovarAsync(request.TokenDeSessao, ct) is { } sessao
            ? Results.Ok(Mobile(sessao))
            : Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sessão expirada", detail: "Faça login novamente.");

    private static async Task<IResult> SairMobile(TokenDeSessaoRequest request, AutenticacaoService auth, CancellationToken ct)
    {
        await auth.SairAsync(request.TokenDeSessao, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Login(LoginRequest request, AutenticacaoService auth, HttpContext http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Senha))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Dados incompletos", detail: "Informe e-mail e senha.");

        return await auth.LoginAsync(request, ct) switch
        {
            ResultadoLogin.Sucesso s => ComCookie(http, s.Sessao),
            ResultadoLogin.Bloqueado b => UsuarioBloqueado(b.Ate),
            _ => CredenciaisInvalidas(),
        };
    }

    private static async Task<IResult> TrocarSenha(TrocarSenhaRequest request, AutenticacaoService auth, ClaimsPrincipal user, CancellationToken ct)
    {
        var erro = await auth.TrocarSenhaAsync(Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!), request, ct);
        return erro is null
            ? Results.NoContent()
            : Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Senha não alterada", detail: erro);
    }

    private static async Task<IResult> Renovar(AutenticacaoService auth, HttpContext http, CancellationToken ct)
    {
        var sessao = await auth.RenovarAsync(http.Request.Cookies[CookieDeSessao], ct);
        if (sessao is null)
        {
            RemoverCookie(http);
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sessão expirada", detail: "Faça login novamente.");
        }
        return ComCookie(http, sessao);
    }

    private static async Task<IResult> Sair(AutenticacaoService auth, HttpContext http, CancellationToken ct)
    {
        await auth.SairAsync(http.Request.Cookies[CookieDeSessao], ct);
        RemoverCookie(http);
        return Results.NoContent();
    }

    private static async Task<IResult> UsuarioAtual(ClaimsPrincipal user, AutenticacaoService auth, CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out var usuarioId))
            return Results.Unauthorized();

        var me = await auth.ObterUsuarioAtualAsync(usuarioId, ct);
        return me is null ? Results.Unauthorized() : Results.Ok(me);
    }

    private static IResult UsuarioBloqueado(DateTimeOffset ate) =>
        Results.Problem(
            statusCode: StatusCodes.Status423Locked,
            title: "Usuário bloqueado",
            detail: $"Usuário bloqueado temporariamente por excesso de tentativas. Tente novamente em {Math.Max(1, (int)Math.Ceiling((ate - DateTimeOffset.UtcNow).TotalMinutes))} minuto(s).");

    private static IResult CredenciaisInvalidas() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Login inválido", detail: "E-mail ou senha inválidos.");

    private static IResult ComCookie(HttpContext http, SessaoEmitida sessao)
    {
        http.Response.Cookies.Append(CookieDeSessao, sessao.TokenDeSessao, OpcoesDoCookie(http, sessao.ManterConectado ? sessao.SessaoExpiraEm : null));
        return Results.Ok(sessao.Resposta);
    }

    private static void RemoverCookie(HttpContext http) => http.Response.Cookies.Delete(CookieDeSessao, OpcoesDoCookie(http, null));

    private static CookieOptions OpcoesDoCookie(HttpContext http, DateTimeOffset? expiraEm)
    {
        var ambiente = http.RequestServices.GetService(typeof(IWebHostEnvironment)) as IWebHostEnvironment;
        return new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Path = CaminhoDoCookie,
            // Em desenvolvimento a API roda em http://localhost; fora dele o cookie só trafega em HTTPS.
            Secure = http.Request.IsHttps || ambiente?.IsDevelopment() != true,
            Expires = expiraEm, // null = cookie de sessão do navegador ("manter conectado" desmarcado)
        };
    }
}
