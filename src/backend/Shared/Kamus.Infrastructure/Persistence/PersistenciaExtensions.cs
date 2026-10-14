using Kamus.Infrastructure.Auditoria;
using Kamus.Infrastructure.MultiTenancy;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Kamus.Infrastructure.Persistence;

/// <summary>Um DbContext com migrations a aplicar na subida (quando há PostgreSQL).</summary>
public sealed record ContextoComMigracoes(string Nome, Func<string, Task> Migrar);

public static partial class PersistenciaExtensions
{
    public const string ConnectionStringName = "Kamus";
    public const string ConnectionStringMigracoes = "KamusMigrations";

    /// <summary>
    /// Sem <c>ConnectionStrings:Kamus</c> os dados ficam <b>em memória</b> (somem ao reiniciar a API) —
    /// é o modo de desenvolvimento em localhost sem PostgreSQL. Com connection string, usa PostgreSQL com RLS.
    /// </summary>
    public static bool UsaBancoEmMemoria(this IConfiguration configuration) =>
        string.IsNullOrWhiteSpace(configuration.GetConnectionString(ConnectionStringName));

    /// <param name="auditar">Grava inclusões/alterações/exclusões no histórico (falso só para o próprio histórico).</param>
    public static IServiceCollection AddKamusDbContext<TContext>(this IServiceCollection services, IConfiguration configuration, string schema, bool auditar = true)
        where TContext : KamusDbContext
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        // Uma "base em memória" por aplicação: isola instâncias (ex.: testes em paralelo) entre si.
        services.TryAddSingleton<InMemoryDatabaseRoot>();

        services.AddDbContext<TContext>((sp, options) =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                options.UseInMemoryDatabase("kamus", sp.GetRequiredService<InMemoryDatabaseRoot>()).UseSnakeCaseNamingConvention();
            else
                options.UseKamusPostgres(connectionString, sp.GetRequiredService<ITenantContext>(), schema);

            if (auditar) options.AddInterceptors(new AuditoriaInterceptor(sp));
        });

        services.AddSingleton(new ContextoComMigracoes(typeof(TContext).Name, cs => MigrarAsync<TContext>(cs, schema)));
        return services;
    }

    /// <summary>
    /// Aplica as migrations de todos os DbContexts registrados, usando <c>ConnectionStrings:KamusMigrations</c>
    /// (dono do banco). A aplicação em si conecta como <c>kamus_app</c>, que não altera estrutura.
    /// </summary>
    public static async Task AplicarMigracoesKamusAsync(this WebApplication app)
    {
        var conexao = app.Configuration.GetConnectionString(ConnectionStringMigracoes);
        if (app.Configuration.UsaBancoEmMemoria() || string.IsNullOrWhiteSpace(conexao)) return;

        var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Kamus.Migracoes");
        foreach (var contexto in app.Services.GetServices<ContextoComMigracoes>())
        {
            await contexto.Migrar(conexao);
            MigracoesAplicadas(log, contexto.Nome);
        }
    }

    private static async Task MigrarAsync<TContext>(string connectionString, string schema) where TContext : KamusDbContext
    {
        var tenant = new TenantContext();
        var options = new DbContextOptionsBuilder<TContext>().UseKamusPostgres(connectionString, tenant, schema).Options;
        await using var db = (TContext)Activator.CreateInstance(typeof(TContext), options, tenant)!;
        await db.Database.MigrateAsync();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Migrations aplicadas: {Contexto}")]
    private static partial void MigracoesAplicadas(ILogger logger, string contexto);
}
