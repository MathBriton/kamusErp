using Kamus.Infrastructure.Persistence;
using Kamus.Modules.Cadastros.Busca;
using Kamus.Modules.Cadastros.Endpoints;
using Kamus.Modules.Cadastros.Persistencia;
using Kamus.Modules.Cadastros.Relatorios;
using Kamus.SharedKernel.Busca;
using Kamus.SharedKernel.Relatorios;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kamus.Modules.Cadastros;

/// <summary>Clientes, fornecedores, vendedores e o catálogo de produtos (grades, cores, produtos e SKUs).</summary>
public static class ModuloCadastros
{
    public static IServiceCollection AddModuloCadastros(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddKamusDbContext<CadastrosDbContext>(configuration, CadastrosDbContext.Schema);
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IProvedorDeBusca, ProvedorDeBuscaCadastros>();
        services.AddScoped<IRelatorio, RelatorioDeClientes>();
        services.AddScoped<IRelatorio, RelatorioDeFornecedores>();
        services.AddScoped<IRelatorio, RelatorioDeProdutos>();
        return services;
    }

    public static IEndpointRouteBuilder MapModuloCadastros(this IEndpointRouteBuilder app) => app.MapPessoas().MapVendedores().MapGradesECores().MapProdutos();
}
