using Kamus.Modules.Identidade.Aplicacao;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Kamus.Modules.Identidade.Endpoints;

/// <summary>Quem entrou com senha provisória só acessa as rotas de autenticação até trocar a senha (403 <c>TROCAR_SENHA</c>).</summary>
internal static class SenhaProvisoriaMiddleware
{
    public const string Codigo = "TROCAR_SENHA";

    public static IApplicationBuilder UseSenhaProvisoria(this IApplicationBuilder app) =>
        app.Use(async (http, next) =>
        {
            if (http.User.HasClaim(TokenService.ClaimTrocarSenha, "true")
                && (http.Request.Path.StartsWithSegments("/api") || http.Request.Path.StartsWithSegments("/hubs"))
                && !http.Request.Path.StartsWithSegments("/api/auth"))
            {
                await Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Troque sua senha",
                    detail: "Sua senha é provisória. Defina uma senha nova para continuar.",
                    extensions: new Dictionary<string, object?> { ["codigo"] = Codigo }).ExecuteAsync(http);
                return;
            }
            await next(http);
        });
}
