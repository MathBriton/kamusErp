using Kamus.Infrastructure.MultiTenancy;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Infrastructure.Persistence;

public static class DbContextOptionsBuilderExtensions
{
    /// <summary>Configuração padrão de todo DbContext do Kamus: PostgreSQL, snake_case e tenant na sessão (RLS).</summary>
    /// <param name="schemaMigrations">Schema do módulo, onde fica a tabela de histórico de migrations.</param>
    public static DbContextOptionsBuilder<TContext> UseKamusPostgres<TContext>(
        this DbContextOptionsBuilder<TContext> builder, string connectionString, ITenantContext tenant, string? schemaMigrations = null)
        where TContext : DbContext
    {
        ((DbContextOptionsBuilder)builder).UseKamusPostgres(connectionString, tenant, schemaMigrations);
        return builder;
    }

    public static DbContextOptionsBuilder UseKamusPostgres(
        this DbContextOptionsBuilder builder, string connectionString, ITenantContext tenant, string? schemaMigrations = null)
    {
        builder.UseNpgsql(connectionString, npgsql =>
            {
                if (schemaMigrations is not null)
                    npgsql.MigrationsHistoryTable("__ef_migrations", schemaMigrations);
            })
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new TenantConnectionInterceptor(tenant));
        return builder;
    }
}
