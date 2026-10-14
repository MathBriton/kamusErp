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
                // WebSockets não enviam header Authorization: o SignalR manda o token na query string.
                o.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var token = ctx.Request.Query["access_token"].ToString();
                        if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                            ctx.Token = token;
                        return Task.CompletedTask;
                    },
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

    public static IEndpointRouteBuilder MapModuloIdentidade(this IEndpointRouteBuilder app) => app.MapAutenticacao().MapEmpresas();

    /// <summary>Valida a empresa ativa (header X-Empresa-Id). Use depois de <c>UseKamusMultiTenancy</c>.</summary>
    public static IApplicationBuilder UseModuloIdentidade(this IApplicationBuilder app) => app.UseEmpresaAtiva();

    /// <summary>Cria os dados de demonstração em Development (rode depois de <c>AplicarMigracoesKamusAsync</c>).</summary>
    public static async Task InicializarModuloIdentidadeAsync(this WebApplication app)
    {
        var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Kamus.Identidade");

        if (app.Configuration.UsaBancoEmMemoria())
            log.BancoEmMemoria();

        if (app.Environment.IsDevelopment())
        {
            await DadosDeDemonstracao.CriarAsync(app.Services);
            log.DemonstracaoPronta(DadosDeDemonstracao.EmailAdmin);
        }
    }
}
