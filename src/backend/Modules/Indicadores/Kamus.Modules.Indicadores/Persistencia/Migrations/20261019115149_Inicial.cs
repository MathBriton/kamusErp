using System;
using Kamus.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Modules.Indicadores.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "indicadores");

            migrationBuilder.CreateTable(
                name: "preferencias_do_resumo",
                schema: "indicadores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    hora = table.Column<int>(type: "integer", nullable: false),
                    dias = table.Column<int>(type: "integer", nullable: false),
                    vendas = table.Column<bool>(type: "boolean", nullable: false),
                    financeiro = table.Column<bool>(type: "boolean", nullable: false),
                    estoque = table.Column<bool>(type: "boolean", nullable: false),
                    condicionais = table.Column<bool>(type: "boolean", nullable: false),
                    crediario = table.Column<bool>(type: "boolean", nullable: false),
                    enviar_sem_movimento = table.Column<bool>(type: "boolean", nullable: false),
                    ultimo_envio = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_preferencias_do_resumo", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_preferencias_do_resumo_tenant_id",
                schema: "indicadores",
                table: "preferencias_do_resumo",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_preferencias_do_resumo_tenant_id_usuario_id",
                schema: "indicadores",
                table: "preferencias_do_resumo",
                columns: new[] { "tenant_id", "usuario_id" },
                unique: true);

            migrationBuilder.EnableTenantRowLevelSecurity("indicadores", "preferencias_do_resumo");
            migrationBuilder.GrantKamusAppRole("indicadores");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "preferencias_do_resumo",
                schema: "indicadores");
        }
    }
}
