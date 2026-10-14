using System.Threading.RateLimiting;
using Kamus.Infrastructure.Persistence;
using Kamus.Modules.Identidade.Aplicacao;
using Kamus.Modules.Identidade.Dominio;
using Kamus.Modules.Identidade.Endpoints;
using Kamus.Modules.Identidade.Persistencia;
using Kamus.Modules.Identidade.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Kamus.Modules.Identidade;

public static class ModuloIdentidade
{
    public static IServiceCollection AddModuloIdentidade(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.Secao).Get<JwtOptions>() ?? new JwtOptions();
        jwt.Validar();
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Secao));

        services.AddKamusDbContext<IdentidadeDbContext>(configuration, IdentidadeDbContext.Schema);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
        services.AddSingleton<TokenService>();
        services.AddScoped<AutenticacaoService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Emissor,
                    ValidAudience = jwt.Audiencia,
                    IssuerSigningKey = TokenService.CriarChave(jwt),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                };
            });
        services.AddAuthorization();

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Máximo de 10 tentativas de login por minuto por IP (além do bloqueio por usuário).
            o.AddPolicy(AutenticacaoEndpoints.PoliticaDeLimiteDoLogin, http =>
                RateLimitPartition.GetFixedWindowLimiter(
                    http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        return services;
    }

    public static IEndpointRouteBuilder MapModuloIdentidade(this IEndpointRouteBuilder app) => app.MapAutenticacao();

    /// <summary>Aplica migrations (se houver conexão de migração) e cria os dados de demonstração em Development.</summary>
    public static async Task InicializarModuloIdentidadeAsync(this WebApplication app)
    {
        var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Kamus.Identidade");

        if (app.Configuration.UsaBancoEmMemoria())
        {
            log.BancoEmMemoria();
        }
        else if (app.Configuration.GetConnectionString("KamusMigrations") is { Length: > 0 } conexaoDeMigracao)
        {
            await MigrarAsync(conexaoDeMigracao);
            log.MigrationsAplicadas();
        }

        if (app.Environment.IsDevelopment())
        {
            await DadosDeDemonstracao.CriarAsync(app.Services);
            log.DemonstracaoPronta(DadosDeDemonstracao.EmailAdmin);
        }
    }

    /// <summary>Migrations rodam com um usuário dono do schema (não com o kamus_app, que só lê/grava dados).</summary>
    private static async Task MigrarAsync(string connectionString)
    {
        var tenant = new Infrastructure.MultiTenancy.TenantContext();
        var options = new DbContextOptionsBuilder<IdentidadeDbContext>()
            .UseKamusPostgres(connectionString, tenant, IdentidadeDbContext.Schema)
            .Options;
        await using var db = new IdentidadeDbContext(options, tenant);
        await db.Database.MigrateAsync();
    }
}
