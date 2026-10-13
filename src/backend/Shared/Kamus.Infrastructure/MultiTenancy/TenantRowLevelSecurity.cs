using Microsoft.EntityFrameworkCore.Migrations;

namespace Kamus.Infrastructure.MultiTenancy;

/// <summary>
/// Segunda camada de isolamento: Row Level Security no PostgreSQL. Mesmo que uma consulta
/// esqueça o filtro do EF (ou use SQL puro), o banco só entrega/aceita linhas do tenant
/// gravado na sessão em <see cref="SessionSetting"/> pelo <see cref="TenantConnectionInterceptor"/>.
/// </summary>
public static class TenantRowLevelSecurity
{
    public const string SessionSetting = "app.tenant_id";
    public const string TenantColumn = "tenant_id";

    public static string EnableSql(string schema, string table) => $"""
        ALTER TABLE {schema}.{table} ENABLE ROW LEVEL SECURITY;
        ALTER TABLE {schema}.{table} FORCE ROW LEVEL SECURITY;
        CREATE POLICY tenant_isolation ON {schema}.{table}
            USING ({TenantColumn} = NULLIF(current_setting('{SessionSetting}', true), '')::uuid)
            WITH CHECK ({TenantColumn} = NULLIF(current_setting('{SessionSetting}', true), '')::uuid);
        """;

    /// <summary>Use nas migrations logo após criar cada tabela de negócio.</summary>
    public static void EnableTenantRowLevelSecurity(this MigrationBuilder migrationBuilder, string schema, string table) =>
        migrationBuilder.Sql(EnableSql(schema, table));
}
