using Kamus.Infrastructure.Persistence;
using Kamus.SharedKernel.Auditoria;
using Kamus.SharedKernel.Relatorios;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kamus.Infrastructure.Auditoria;

public static class AuditoriaExtensions
{
    public static IServiceCollection AddKamusAuditoria(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IUsuarioAtual, UsuarioAtualHttp>();
        services.AddScoped<FonteDeAutoria>();
        services.AddScoped<IRegistradorDeEventos, RegistradorDeEventos>();
        services.AddScoped<IRelatorio, RelatorioHistoricoDeAlteracoes>();
        services.AddKamusDbContext<AuditoriaDbContext>(configuration, AuditoriaDbContext.Schema, auditar: false);
        return services;
    }
}
