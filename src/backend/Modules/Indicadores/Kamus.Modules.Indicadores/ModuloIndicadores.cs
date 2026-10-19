using Kamus.Infrastructure.Persistence;
using Kamus.Modules.Indicadores.Aplicacao;
using Kamus.Modules.Indicadores.Endpoints;
using Kamus.Modules.Indicadores.Persistencia;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kamus.Modules.Indicadores;

/// <summary>Dashboard (junta os indicadores de vendas, financeiro e estoque) e resumo diário no sininho.</summary>
public static class ModuloIndicadores
{
    public static IServiceCollection AddModuloIndicadores(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddKamusDbContext<IndicadoresDbContext>(configuration, IndicadoresDbContext.Schema);
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<Painel>();
        services.AddScoped<ResumoDiario>();
        services.AddScoped<GeradorDeResumos>();
        services.AddHostedService<RotinaDoResumoDiario>();
        return services;
    }

    public static IEndpointRouteBuilder MapModuloIndicadores(this IEndpointRouteBuilder app) => app.MapIndicadores();
}
