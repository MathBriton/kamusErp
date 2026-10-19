using Kamus.Infrastructure.Importacao;
using Kamus.Infrastructure.Persistence;
using Kamus.Modules.Estoque.Aplicacao;
using Kamus.Modules.Estoque.Endpoints;
using Kamus.Modules.Estoque.Importacao;
using Kamus.Modules.Estoque.Persistencia;
using Kamus.Modules.Estoque.Relatorios;
using Kamus.SharedKernel.Estoque;
using Kamus.SharedKernel.Relatorios;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kamus.Modules.Estoque;

/// <summary>Lojas, saldo por SKU e loja, movimentações (kardex) e custo médio.</summary>
public static class ModuloEstoque
{
    public static IServiceCollection AddModuloEstoque(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddKamusDbContext<EstoqueDbContext>(configuration, EstoqueDbContext.Schema);
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<Lojas>();
        services.AddScoped<IMovimentacaoDeEstoque, MovimentacaoDeEstoque>();
        services.AddScoped<Kamus.SharedKernel.Indicadores.IIndicadoresDeEstoque, IndicadoresDeEstoque>();
        services.AddScoped<IRelatorio, RelatorioDePosicao>();
        services.AddScoped<IRelatorio, RelatorioDeInventario>();
        services.AddScoped<IImportador, ImportadorDeEstoqueInicial>();
        return services;
    }

    public static IEndpointRouteBuilder MapModuloEstoque(this IEndpointRouteBuilder app) => app.MapLojas().MapMovimentacoes().MapPosicao().MapEtiquetas().MapInventarios().MapNotas();
}
