using System;
using Kamus.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Infrastructure.Auditoria.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "auditoria");

            migrationBuilder.CreateTable(
                name: "registros",
                schema: "auditoria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ocorrido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    evento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    entidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    entidade_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usuario_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    alteracoes = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_registros", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_registros_entidade_entidade_id",
                schema: "auditoria",
                table: "registros",
                columns: new[] { "entidade", "entidade_id" });

            migrationBuilder.CreateIndex(
                name: "ix_registros_tenant_id",
                schema: "auditoria",
                table: "registros",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_registros_tenant_id_ocorrido_em",
                schema: "auditoria",
                table: "registros",
                columns: new[] { "tenant_id", "ocorrido_em" });

            // Histórico isolado por tenant (RLS) e somente-inclusão para a aplicação.
            migrationBuilder.EnableTenantRowLevelSecurity("auditoria", "registros");
            migrationBuilder.GrantKamusAppRole("auditoria");
            migrationBuilder.SomenteInclusaoParaKamusApp("auditoria", "registros");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registros",
                schema: "auditoria");
        }
    }
}
