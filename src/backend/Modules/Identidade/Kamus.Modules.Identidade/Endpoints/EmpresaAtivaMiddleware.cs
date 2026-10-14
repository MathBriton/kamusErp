using System.Security.Claims;
using Kamus.Infrastructure.MultiTenancy;
using Kamus.Modules.Identidade.Dominio;
using Kamus.Modules.Identidade.Persistencia;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Kamus.Modules.Identidade.Endpoints;

/// <summary>
/// Lê o header <c>X-Empresa-Id</c> (guid da empresa ou <c>todas</c>) e só o aceita se o usuário tiver acesso.
/// O frontend nunca é a fonte da verdade. Deve vir depois de <c>UseKamusMultiTenancy</c>.
/// </summary>
internal static class EmpresaAtivaMiddleware
{
    public const string Header = "X-Empresa-Id";
    public const string Consolidado = "todas";

    public static IApplicationBuilder UseEmpresaAtiva(this IApplicationBuilder app) =>
        app.Use(async (http, next) =>
        {
            var valor = http.Request.Headers[Header].ToString();
            if (string.IsNullOrWhiteSpace(valor)
                || http.User.Identity?.IsAuthenticated != true
                || http.Request.Path.StartsWithSegments("/api/auth"))
            {
                await next(http);
                return;
            }

            var erro = await ValidarAsync(http, valor.Trim());
            if (erro is not null)
            {
                await erro.ExecuteAsync(http);
                return;
            }
            await next(http);
        });

    private static async Task<IResult?> ValidarAsync(HttpContext http, string valor)
    {
        if (!Guid.TryParse(http.User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var usuarioId))
            return Results.Unauthorized();

        var db = http.RequestServices.GetRequiredService<IdentidadeDbContext>();
        var empresaAtiva = http.RequestServices.GetRequiredService<EmpresaAtiva>();
        var ct = http.RequestAborted;

        if (string.Equals(valor, Consolidado, StringComparison.OrdinalIgnoreCase))
        {
            if (!HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method))
                return Problema(StatusCodes.Status400BadRequest, "Selecione uma empresa específica: a visão consolidada é somente para consulta.");

            var acessos = await db.AcessosEmpresa.AsNoTracking().Where(a => a.UsuarioId == usuarioId).ToListAsync(ct);
            if (acessos.Count == 0 || acessos.Any(a => a.Perfil != Perfil.Dono))
                return Problema(StatusCodes.Status403Forbidden, "A visão consolidada é exclusiva do perfil Dono.");

            empresaAtiva.DefinirConsolidado(acessos.Select(a => a.EmpresaId).ToList());
            return null;
        }

        if (!Guid.TryParse(valor, out var empresaId))
            return Problema(StatusCodes.Status400BadRequest, $"Valor inválido no header {Header}: use o id da empresa ou '{Consolidado}'.");

        var acesso = await db.AcessosEmpresa.AsNoTracking().SingleOrDefaultAsync(a => a.UsuarioId == usuarioId && a.EmpresaId == empresaId, ct);
        if (acesso is null)
            return Problema(StatusCodes.Status403Forbidden, "Você não tem acesso a esta empresa.");

        empresaAtiva.Definir(empresaId, acesso.Perfil.ToString());
        return null;
    }

    private static IResult Problema(int status, string detalhe) =>
        Results.Problem(statusCode: status, title: "Empresa inválida", detail: detalhe);
}
