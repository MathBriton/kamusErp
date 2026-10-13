using Kamus.Infrastructure.MultiTenancy;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Kamus.Infrastructure.Persistence;

public static class DbContextOptionsBuilderExtensions
{
    /// <summary>Configuração padrão de todo DbContext do Kamus: PostgreSQL, snake_case e tenant na sessão (RLS).</summary>
    public static DbContextOptionsBuilder<TContext> UseKamusPostgres<TContext>(
        this DbContextOptionsBuilder<TContext> builder, string connectionString, ITenantContext tenant)
        where TContext : DbContext
    {
        builder.UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new TenantConnectionInterceptor(tenant));
        return builder;
    }
}
