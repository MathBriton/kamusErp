using System;
using Kamus.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Modules.Identidade.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identidade");

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "identidade",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "empresas",
                schema: "identidade",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    razao_social = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    nome_fantasia = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_empresas", x => x.id);
                    table.ForeignKey(
                        name: "fk_empresas_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "identidade",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                schema: "identidade",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    senha_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    falhas_de_login = table.Column<int>(type: "integer", nullable: false),
                    bloqueado_ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_login_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id);
                    table.ForeignKey(
                        name: "fk_usuarios_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "identidade",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "acessos_empresa",
                schema: "identidade",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acessos_empresa", x => x.id);
                    table.ForeignKey(
                        name: "fk_acessos_empresa_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalSchema: "identidade",
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_acessos_empresa_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "identidade",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "identidade",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    manter_conectado = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revogado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    substituido_por_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "identidade",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_acessos_empresa_empresa_id",
                schema: "identidade",
                table: "acessos_empresa",
                column: "empresa_id");

            migrationBuilder.CreateIndex(
                name: "ix_acessos_empresa_tenant_id",
                schema: "identidade",
                table: "acessos_empresa",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_acessos_empresa_usuario_id_empresa_id",
                schema: "identidade",
                table: "acessos_empresa",
                columns: new[] { "usuario_id", "empresa_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_empresas_tenant_id",
                schema: "identidade",
                table: "empresas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_empresas_tenant_id_cnpj",
                schema: "identidade",
                table: "empresas",
                columns: new[] { "tenant_id", "cnpj" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                schema: "identidade",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_usuario_id",
                schema: "identidade",
                table: "refresh_tokens",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_email",
                schema: "identidade",
                table: "usuarios",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_tenant_id",
                schema: "identidade",
                table: "usuarios",
                column: "tenant_id");

            // Dados do cliente: isolados por tenant também no banco (RLS).
            // tenants, usuarios e refresh_tokens ficam fora: são lidos no login, antes de o tenant ser conhecido.
            migrationBuilder.EnableTenantRowLevelSecurity("identidade", "empresas");
            migrationBuilder.EnableTenantRowLevelSecurity("identidade", "acessos_empresa");
            migrationBuilder.GrantKamusAppRole("identidade");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "acessos_empresa",
                schema: "identidade");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "identidade");

            migrationBuilder.DropTable(
                name: "empresas",
                schema: "identidade");

            migrationBuilder.DropTable(
                name: "usuarios",
                schema: "identidade");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "identidade");
        }
    }
}
