using Kamus.Modules.Licenciamento.Aplicacao;
using Kamus.Modules.Licenciamento.Dominio;
using Kamus.Modules.Licenciamento.Persistencia;
using Kamus.SharedKernel.MultiTenancy;
using Kamus.SharedKernel.Tempo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.Modules.Licenciamento.Endpoints;

/// <summary>
/// Bloqueia (HTTP 402) as requisições de um cliente com licença suspensa ou cancelada. Continuam liberados:
/// login/sessão, a própria situação da licença, notificações, empresas e a exportação de relatórios
/// (o cliente sempre pode levar os dados dele). Cliente sem licença cadastrada não é bloqueado.
/// </summary>
internal static class LicencaMiddleware
{
    private static readonly string[] SempreLiberados =
        ["/api/auth", "/api/licenca", "/api/notificacoes", "/api/empresas", "/api/relatorios", "/api/versao", "/hubs", "/health"];

    public static IApplicationBuilder UseLicenca(this IApplicationBuilder app) =>
        app.Use(async (http, next) =>
        {
            var tenantId = http.RequestServices.GetRequiredService<ITenantContext>().TenantId;
            if (tenantId is null || !http.Request.Path.StartsWithSegments("/api") || SempreLiberados.Any(p => http.Request.Path.StartsWithSegments(p)))
            {
                await next(http);
                return;
            }

            var sp = http.RequestServices;
            var licenca = await sp.GetRequiredService<CacheDeLicencas>().ObterAsync(tenantId.Value, sp.GetRequiredService<LicenciamentoDbContext>(), http.RequestAborted);
            var hoje = CalendarioBrasilia.Hoje(sp.GetRequiredService<TimeProvider>());
            var estado = licenca?.EstadoEm(hoje);
            if (licenca is null || estado is not (EstadoLicenca.Suspensa or EstadoLicenca.Cancelada))
            {
                await next(http);
                return;
            }

            var situacao = SituacaoDaLicenca.De(licenca, hoje);
            var codigo = estado == EstadoLicenca.Cancelada ? SituacaoDaLicenca.CodigoCancelada : SituacaoDaLicenca.CodigoSuspensa;
            await Results.Problem(
                statusCode: StatusCodes.Status402PaymentRequired,
                title: estado == EstadoLicenca.Cancelada ? "Licença cancelada" : "Licença suspensa",
                detail: situacao.Mensagem,
                extensions: new Dictionary<string, object?> { ["codigo"] = codigo }).ExecuteAsync(http);
        });
}
