using Kamus.Infrastructure.Persistence;
using Kamus.Modules.Financeiro.Persistencia;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kamus.Modules.Financeiro;

/// <summary>Contas financeiras, categorias, contas a pagar e a receber (títulos, parcelas e baixas) e fluxo de caixa.</summary>
public static class ModuloFinanceiro
{
    public static IServiceCollection AddModuloFinanceiro(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddKamusDbContext<FinanceiroDbContext>(configuration, FinanceiroDbContext.Schema);
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }

    public static IEndpointRouteBuilder MapModuloFinanceiro(this IEndpointRouteBuilder app) => app;
}
