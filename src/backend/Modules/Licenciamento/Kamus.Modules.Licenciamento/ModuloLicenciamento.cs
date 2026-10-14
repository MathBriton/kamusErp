using System.Threading.RateLimiting;
using Kamus.Infrastructure.Persistence;
using Kamus.Modules.Licenciamento.Administracao;
using Kamus.Modules.Licenciamento.Aplicacao;
using Kamus.Modules.Licenciamento.Dominio;
using Kamus.Modules.Licenciamento.Endpoints;
using Kamus.Modules.Licenciamento.Persistencia;
using Kamus.Modules.Licenciamento.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Kamus.Modules.Licenciamento;

public static class ModuloLicenciamento
{
    public static IServiceCollection AddModuloLicenciamento(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddKamusDbContext<LicenciamentoDbContext>(configuration, LicenciamentoDbContext.Schema, auditar: false);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<CacheDeLicencas>();
        services.AddScoped<AvisoAosDonos>();
        services.AddScoped<Kamus.SharedKernel.Clientes.ILimitesDoCliente, LimitesDaLicenca>();
        services.AddScoped<VerificadorDeVencimentos>();
        services.AddHostedService<RotinaDeVencimentos>();

        var admin = configuration.GetSection(AdminOptions.Secao).Get<AdminOptions>() ?? new AdminOptions();
        admin.Validar();
        services.Configure<AdminOptions>(configuration.GetSection(AdminOptions.Secao));
        if (admin.Habilitado) AddPainelAdministrativo(services, admin);
        return services;
    }

    private static void AddPainelAdministrativo(IServiceCollection services, AdminOptions admin)
    {
        services.AddSingleton<IPasswordHasher<Administrador>, PasswordHasher<Administrador>>();
        services.AddScoped<AutenticacaoAdmin>();

        // Esquema próprio: outra chave e outra audiência. O token do cliente não serve aqui, e vice-versa.
        services.AddAuthentication().AddJwtBearer(AdminOptions.Esquema, o =>
        {
            o.MapInboundClaims = false;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = admin.Emissor,
                ValidAudience = admin.Audiencia,
                IssuerSigningKey = AutenticacaoAdmin.Chave(admin),
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "name",
            };
        });
        services.AddAuthorizationBuilder().AddPolicy(AdminOptions.Politica, p => p
            .AddAuthenticationSchemes(AdminOptions.Esquema)
            .RequireAuthenticatedUser()
            .RequireClaim(AutenticacaoAdmin.ClaimPapel, AutenticacaoAdmin.PapelSuperAdmin));

        services.AddRateLimiter(o => o.AddPolicy(PainelEndpoints.PoliticaDeLimiteDoLogin, http =>
            RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
    }

    /// <summary>Bloqueio por licença e filtro de IP do painel. Use depois de <c>UseKamusMultiTenancy</c> e antes de <c>UseAuthorization</c>.</summary>
    public static IApplicationBuilder UseModuloLicenciamento(this IApplicationBuilder app)
    {
        var admin = app.ApplicationServices.GetRequiredService<IOptions<AdminOptions>>().Value;
        var ips = admin.ListaDeIps();
        if (ips.Count > 0)
        {
            app.Use(async (http, next) =>
            {
                // Fora da lista, o painel "não existe" (404), para não revelar que há algo ali.
                if (http.Request.Path.StartsWithSegments(AdminOptions.Prefixo) && !ips.Contains(http.Connection.RemoteIpAddress?.ToString() ?? ""))
                {
                    http.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
                await next(http);
            });
        }
        return app.UseLicenca();
    }

    public static IEndpointRouteBuilder MapModuloLicenciamento(this IEndpointRouteBuilder app)
    {
        app.MapLicenca();
        if (app.ServiceProvider.GetRequiredService<IOptions<AdminOptions>>().Value.Habilitado)
            app.MapPainelAdministrativo();
        return app;
    }

    /// <summary>
    /// Development: licenças de demonstração e o SuperAdmin de teste. Fora dele: cria o primeiro SuperAdmin a partir de
    /// <c>Admin:EmailInicial</c>/<c>Admin:SenhaInicial</c>, se configurados. Rode depois de <c>InicializarModuloIdentidadeAsync</c>.
    /// </summary>
    public static async Task InicializarModuloLicenciamentoAsync(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
            await LicencasDeDemonstracao.CriarAsync(app.Services);

        var admin = app.Services.GetRequiredService<IOptions<AdminOptions>>().Value;
        if (!admin.Habilitado) return;

        var (email, senha) = app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(app.Configuration["Seed:SenhaSuperAdmin"])
            ? (LicencasDeDemonstracao.EmailSuperAdmin, app.Configuration["Seed:SenhaSuperAdmin"]!)
            : (admin.EmailInicial, admin.SenhaInicial);
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha)) return;

        await using var escopo = app.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<AutenticacaoAdmin>().GarantirAsync("Super Admin", email, senha, CancellationToken.None);
    }
}
