using System.Data.Common;
using Kamus.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kamus.Infrastructure.MultiTenancy;

/// <summary>Grava o tenant atual na sessão do PostgreSQL a cada abertura de conexão (inclusive conexões do pool).</summary>
public sealed class TenantConnectionInterceptor(ITenantContext tenant) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = CriarComando(connection);
        cmd.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var cmd = CriarComando(connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CriarComando(DbConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT set_config('{TenantRowLevelSecurity.SessionSetting}', @tenant, false)";
        var p = cmd.CreateParameter();
        p.ParameterName = "tenant";
        p.Value = tenant.TenantId?.ToString() ?? "";
        cmd.Parameters.Add(p);
        return cmd;
    }
}
