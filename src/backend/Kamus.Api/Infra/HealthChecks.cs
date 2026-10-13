using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Kamus.Api.Infra;

internal static class HealthChecks
{
    public static IServiceCollection AddKamusHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Kamus")
            ?? throw new InvalidOperationException("ConnectionStrings:Kamus não configurada.");

        services.AddHealthChecks()
            .AddCheck("postgres", new PostgresHealthCheck(connectionString), tags: ["db"]);
        return services;
    }
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
