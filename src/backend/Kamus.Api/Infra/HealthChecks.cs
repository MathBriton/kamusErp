using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Kamus.Api.Infra;

internal static class HealthChecks
{
    public static IServiceCollection AddKamusHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Kamus");

        // Sem connection string a API sobe mesmo assim (desenvolvimento em localhost sem PostgreSQL).
        IHealthCheck check = string.IsNullOrWhiteSpace(connectionString)
            ? new BancoNaoConfiguradoHealthCheck()
            : new PostgresHealthCheck(connectionString);

        services.AddHealthChecks().AddCheck("postgres", check, tags: ["db"]);
        return services;
    }
}

internal sealed class BancoNaoConfiguradoHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(HealthCheckResult.Degraded("Banco não configurado (ConnectionStrings:Kamus vazia): API rodando sem PostgreSQL."));
}

internal sealed class PostgresHealthCheck(string connectionString) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand("SELECT 1", conn);
            await cmd.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException or System.Net.Sockets.SocketException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL indisponível.", ex);
        }
    }
}
