using Kamus.Infrastructure.Persistence;
using Kamus.Modules.Financeiro.Aplicacao;
using Kamus.Modules.Financeiro.Endpoints;
using Kamus.Modules.Financeiro.Persistencia;
using Kamus.Modules.Financeiro.Relatorios;
using Kamus.SharedKernel.Financeiro;
using Kamus.SharedKernel.Relatorios;
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
        services.AddScoped<IContasAPagar, ContasAPagar>();
        services.AddScoped<IContasAReceber, ContasAReceber>();
        services.AddScoped<Crediario>();
        services.AddScoped<ICrediario>(sp => sp.GetRequiredService<Crediario>());
        services.AddScoped<IRelatorio, RelatorioDeInadimplencia>();
        services.AddScoped<IRelatorio, RelatorioDeTitulos>();
        services.AddScoped<IRelatorio, RelatorioDeFluxo>();
        return services;
    }

    public static IEndpointRouteBuilder MapModuloFinanceiro(this IEndpointRouteBuilder app) => app.MapContas().MapCategorias().MapTitulos().MapFluxo().MapCrediario();
}
