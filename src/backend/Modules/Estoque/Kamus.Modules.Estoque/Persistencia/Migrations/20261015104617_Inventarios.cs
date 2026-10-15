using System;
using Kamus.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Modules.Estoque.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Inventarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventarios",
                schema: "estoque",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    loja_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    aberto_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    aberto_por = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    encerrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concluido_por = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    zerou_nao_contados = table.Column<bool>(type: "boolean", nullable: false),
                    operacao_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventarios", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventarios_lojas_loja_id",
                        column: x => x.loja_id,
                        principalSchema: "estoque",
                        principalTable: "lojas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "itens_de_inventario",
                schema: "estoque",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    produto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    saldo_na_abertura = table.Column<int>(type: "integer", nullable: false),
                    contado = table.Column<int>(type: "integer", nullable: true),
                    contado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_itens_de_inventario", x => x.id);
                    table.ForeignKey(
                        name: "fk_itens_de_inventario_inventarios_inventario_id",
                        column: x => x.inventario_id,
                        principalSchema: "estoque",
                        principalTable: "inventarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inventarios_loja_id",
                schema: "estoque",
                table: "inventarios",
                column: "loja_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventarios_tenant_id",
                schema: "estoque",
                table: "inventarios",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventarios_tenant_id_loja_id_aberto_em",
                schema: "estoque",
                table: "inventarios",
                columns: new[] { "tenant_id", "loja_id", "aberto_em" });

            migrationBuilder.CreateIndex(
                name: "ix_itens_de_inventario_inventario_id_sku_id",
                schema: "estoque",
                table: "itens_de_inventario",
                columns: new[] { "inventario_id", "sku_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_itens_de_inventario_tenant_id",
                schema: "estoque",
                table: "itens_de_inventario",
                column: "tenant_id");

            foreach (var tabela in new[] { "inventarios", "itens_de_inventario" })
                migrationBuilder.EnableTenantRowLevelSecurity("estoque", tabela);
            migrationBuilder.GrantKamusAppRole("estoque");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "itens_de_inventario",
                schema: "estoque");

            migrationBuilder.DropTable(
                name: "inventarios",
                schema: "estoque");
        }
    }
}
