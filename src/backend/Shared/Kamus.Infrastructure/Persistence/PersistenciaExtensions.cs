using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kamus.Infrastructure.Persistence;

public static class PersistenciaExtensions
{
    public const string ConnectionStringName = "Kamus";

    /// <summary>
    /// Sem <c>ConnectionStrings:Kamus</c> os dados ficam <b>em memória</b> (somem ao reiniciar a API) —
    /// é o modo de desenvolvimento em localhost sem PostgreSQL. Com connection string, usa PostgreSQL com RLS.
    /// </summary>
    public static bool UsaBancoEmMemoria(this IConfiguration configuration) =>
        string.IsNullOrWhiteSpace(configuration.GetConnectionString(ConnectionStringName));

    public static IServiceCollection AddKamusDbContext<TContext>(this IServiceCollection services, IConfiguration configuration, string schema)
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
        });
        return services;
    }
}
