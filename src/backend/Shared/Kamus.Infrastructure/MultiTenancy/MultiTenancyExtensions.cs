using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.Infrastructure.MultiTenancy;

public static class MultiTenancyExtensions
{
    /// <summary>Claim do token com o tenant do usuário.</summary>
    public const string TenantClaim = "tenant_id";

    public static IServiceCollection AddKamusMultiTenancy(this IServiceCollection services)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<EmpresaAtiva>();
        services.AddScoped<IEmpresaAtiva>(sp => sp.GetRequiredService<EmpresaAtiva>());
        return services;
    }

    /// <summary>Define o tenant da requisição a partir do token. Deve vir depois de <c>UseAuthentication</c>.</summary>
    public static IApplicationBuilder UseKamusMultiTenancy(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.User.Identity?.IsAuthenticated == true
                && Guid.TryParse(context.User.FindFirst(TenantClaim)?.Value, out var tenantId))
            {
                context.RequestServices.GetRequiredService<TenantContext>().Definir(tenantId);
            }
            await next(context);
        });
}
