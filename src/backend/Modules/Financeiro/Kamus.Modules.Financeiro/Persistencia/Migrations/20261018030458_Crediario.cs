using System;
using Kamus.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Modules.Financeiro.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Crediario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "crediario",
                schema: "financeiro",
                table: "titulos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "configuracoes_do_crediario",
                schema: "financeiro",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    multa_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    juros_mensal_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    dias_para_bloquear = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes_do_crediario", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "limites_de_credito",
                schema: "financeiro",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    limite = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    bloqueado = table.Column<bool>(type: "boolean", nullable: false),
                    observacao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    atualizado_por = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_limites_de_credito", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_titulos_tenant_id_pessoa_id_crediario",
                schema: "financeiro",
                table: "titulos",
                columns: new[] { "tenant_id", "pessoa_id", "crediario" });

            migrationBuilder.CreateIndex(
                name: "ix_configuracoes_do_crediario_tenant_id",
                schema: "financeiro",
                table: "configuracoes_do_crediario",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_limites_de_credito_tenant_id",
                schema: "financeiro",
                table: "limites_de_credito",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_limites_de_credito_tenant_id_cliente_id",
                schema: "financeiro",
                table: "limites_de_credito",
                columns: new[] { "tenant_id", "cliente_id" },
                unique: true);

            foreach (var tabela in new[] { "limites_de_credito", "configuracoes_do_crediario" })
                migrationBuilder.EnableTenantRowLevelSecurity("financeiro", tabela);
            migrationBuilder.GrantKamusAppRole("financeiro");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracoes_do_crediario",
                schema: "financeiro");

            migrationBuilder.DropTable(
                name: "limites_de_credito",
                schema: "financeiro");

            migrationBuilder.DropIndex(
                name: "ix_titulos_tenant_id_pessoa_id_crediario",
                schema: "financeiro",
                table: "titulos");

            migrationBuilder.DropColumn(
                name: "crediario",
                schema: "financeiro",
                table: "titulos");
        }
    }
}
