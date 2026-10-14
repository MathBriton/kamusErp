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

    /// <summary>Papel com que a aplicação conecta (sem superusuário, para o RLS valer).</summary>
    public const string AppRole = "kamus_app";

    /// <summary>
    /// Permissões de leitura/escrita do <see cref="AppRole"/> no schema do módulo (se o papel existir).
    /// Migrations rodam com o dono do schema; a aplicação nunca altera estrutura.
    /// </summary>
    public static string GrantAppRoleSql(string schema) => $$"""
        DO $$
        BEGIN
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{{AppRole}}') THEN
                GRANT USAGE ON SCHEMA {{schema}} TO {{AppRole}};
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {{schema}} TO {{AppRole}};
                ALTER DEFAULT PRIVILEGES IN SCHEMA {{schema}} GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {{AppRole}};
            END IF;
        END
        $$;
        """;

    public static void GrantKamusAppRole(this MigrationBuilder migrationBuilder, string schema) =>
        migrationBuilder.Sql(GrantAppRoleSql(schema));

    /// <summary>Tabela somente-inclusão para o <see cref="AppRole"/> (ex.: histórico de alterações).</summary>
    public static string SomenteInclusaoSql(string schema, string table) => $$"""
        DO $$
        BEGIN
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{{AppRole}}') THEN
                REVOKE UPDATE, DELETE, TRUNCATE ON {{schema}}.{{table}} FROM {{AppRole}};
            END IF;
        END
        $$;
        """;

    public static void SomenteInclusaoParaKamusApp(this MigrationBuilder migrationBuilder, string schema, string table) =>
        migrationBuilder.Sql(SomenteInclusaoSql(schema, table));

    /// <summary>Use nas migrations logo após criar cada tabela de negócio.</summary>
    public static void EnableTenantRowLevelSecurity(this MigrationBuilder migrationBuilder, string schema, string table) =>
        migrationBuilder.Sql(EnableSql(schema, table));
}
