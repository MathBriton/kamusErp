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
        services.AddScoped<Kamus.SharedKernel.Indicadores.IIndicadoresDeVendas, Aplicacao.IndicadoresDeVendas>();
        services.AddScoped<Kamus.SharedKernel.Indicadores.IProgressoDaImplantacao, Aplicacao.ProgressoDaImplantacao>();
        services.AddScoped<IRelatorio, RelatorioDeVendas>();
        services.AddScoped<IRelatorio, RelatorioDeComissoes>();
        services.AddScoped<IRelatorio, RelatorioDeVales>();
        services.AddScoped<IRelatorio, RelatorioDeCondicionais>();
        services.AddScoped<IRelatorio, RelatorioDeTrocas>();
        services.AddScoped<IRelatorio, RelatorioDeFormas>();
        services.AddScoped<IRelatorio, RelatorioDeFechamentos>();
        services.AddScoped<IRelatorio, RelatorioDeVendasAgrupadas>();
        services.AddScoped<IRelatorio, RelatorioCurvaAbc>();
        return services;
    }

    public static IEndpointRouteBuilder MapModuloVendas(this IEndpointRouteBuilder app) => app.MapFormas().MapVendas().MapTrocas().MapVales().MapFechamentos().MapCondicionais();
}
