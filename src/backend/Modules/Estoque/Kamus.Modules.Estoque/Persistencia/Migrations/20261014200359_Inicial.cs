using System;
using Kamus.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Modules.Estoque.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "estoque");

            migrationBuilder.CreateTable(
                name: "lojas",
                schema: "estoque",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ativa = table.Column<bool>(type: "boolean", nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lojas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "movimentos",
                schema: "estoque",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequencia = table.Column<int>(type: "integer", nullable: false),
                    loja_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    produto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    custo_unitario = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    saldo_apos = table.Column<int>(type: "integer", nullable: false),
                    custo_medio_apos = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    motivo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    documento = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    loja_relacionada_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usuario_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ocorrido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movimentos", x => x.id);
                    table.ForeignKey(
                        name: "fk_movimentos_lojas_loja_id",
                        column: x => x.loja_id,
                        principalSchema: "estoque",
                        principalTable: "lojas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "saldos",
                schema: "estoque",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    loja_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    produto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    custo_medio = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    versao = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saldos", x => x.id);
                    table.ForeignKey(
                        name: "fk_saldos_lojas_loja_id",
                        column: x => x.loja_id,
                        principalSchema: "estoque",
                        principalTable: "lojas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lojas_tenant_id",
                schema: "estoque",
                table: "lojas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_lojas_tenant_id_empresa_id_nome",
                schema: "estoque",
                table: "lojas",
                columns: new[] { "tenant_id", "empresa_id", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_movimentos_loja_id",
                schema: "estoque",
                table: "movimentos",
                column: "loja_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimentos_operacao_id",
                schema: "estoque",
                table: "movimentos",
                column: "operacao_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimentos_tenant_id",
                schema: "estoque",
                table: "movimentos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimentos_tenant_id_produto_id_ocorrido_em",
                schema: "estoque",
                table: "movimentos",
                columns: new[] { "tenant_id", "produto_id", "ocorrido_em" });

            migrationBuilder.CreateIndex(
                name: "ix_movimentos_tenant_id_sku_id_ocorrido_em",
                schema: "estoque",
                table: "movimentos",
                columns: new[] { "tenant_id", "sku_id", "ocorrido_em" });

            migrationBuilder.CreateIndex(
                name: "ix_saldos_loja_id",
                schema: "estoque",
                table: "saldos",
                column: "loja_id");

            migrationBuilder.CreateIndex(
                name: "ix_saldos_tenant_id",
                schema: "estoque",
                table: "saldos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_saldos_tenant_id_loja_id_sku_id",
                schema: "estoque",
                table: "saldos",
                columns: new[] { "tenant_id", "loja_id", "sku_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_saldos_tenant_id_produto_id",
                schema: "estoque",
                table: "saldos",
                columns: new[] { "tenant_id", "produto_id" });

            foreach (var tabela in new[] { "lojas", "saldos", "movimentos" })
                migrationBuilder.EnableTenantRowLevelSecurity("estoque", tabela);
            migrationBuilder.GrantKamusAppRole("estoque");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "movimentos",
                schema: "estoque");

            migrationBuilder.DropTable(
                name: "saldos",
                schema: "estoque");

            migrationBuilder.DropTable(
                name: "lojas",
                schema: "estoque");
        }
    }
}
