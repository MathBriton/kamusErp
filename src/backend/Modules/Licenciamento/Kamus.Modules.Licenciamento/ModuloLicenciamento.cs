using Kamus.Infrastructure.Persistence;
using Kamus.Modules.Licenciamento.Aplicacao;
using Kamus.Modules.Licenciamento.Endpoints;
using Kamus.Modules.Licenciamento.Persistencia;
using Kamus.Modules.Licenciamento.Seed;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Kamus.Modules.Licenciamento;

public static class ModuloLicenciamento
{
    public static IServiceCollection AddModuloLicenciamento(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddKamusDbContext<LicenciamentoDbContext>(configuration, LicenciamentoDbContext.Schema, auditar: false);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<CacheDeLicencas>();
        services.AddScoped<VerificadorDeVencimentos>();
        services.AddHostedService<RotinaDeVencimentos>();
        return services;
    }

    /// <summary>Bloqueio por licença. Use depois de <c>UseKamusMultiTenancy</c> e antes de <c>UseAuthorization</c>.</summary>
    public static IApplicationBuilder UseModuloLicenciamento(this IApplicationBuilder app) => app.UseLicenca();

    public static IEndpointRouteBuilder MapModuloLicenciamento(this IEndpointRouteBuilder app) => app.MapLicenca();

    /// <summary>Cria as licenças de demonstração em Development (rode depois de <c>InicializarModuloIdentidadeAsync</c>).</summary>
    public static async Task InicializarModuloLicenciamentoAsync(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
            await LicencasDeDemonstracao.CriarAsync(app.Services);
    }
}
