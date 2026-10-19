using Kamus.Infrastructure.Persistence;
using Kamus.Modules.Vendas.Endpoints;
using Kamus.Modules.Vendas.Persistencia;
using Kamus.Modules.Vendas.Relatorios;
using Kamus.SharedKernel.Relatorios;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kamus.Modules.Vendas;

/// <summary>Registro de vendas no backoffice, formas de pagamento e comissões.</summary>
public static class ModuloVendas
{
    public static IServiceCollection AddModuloVendas(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddKamusDbContext<VendasDbContext>(configuration, VendasDbContext.Schema);
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<Aplicacao.RegistroDeVendas>();
        services.AddScoped<IRelatorio, RelatorioDeVendas>();
        services.AddScoped<IRelatorio, RelatorioDeComissoes>();
        services.AddScoped<IRelatorio, RelatorioDeVales>();
        services.AddScoped<IRelatorio, RelatorioDeCondicionais>();
        return services;
    }

    public static IEndpointRouteBuilder MapModuloVendas(this IEndpointRouteBuilder app) => app.MapFormas().MapVendas().MapTrocas().MapVales().MapFechamentos().MapCondicionais();
}
