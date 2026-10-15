using System;
using Kamus.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Modules.Estoque.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class NotasDeEntrada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notas",
                schema: "estoque",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chave = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: false),
                    numero = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    serie = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    emitida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fornecedor_documento = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    fornecedor_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    valor_total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    autorizada = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    importada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    importada_por = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    lancada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lancada_por = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    loja_id = table.Column<Guid>(type: "uuid", nullable: true),
                    operacao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fornecedor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xml = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vinculos_de_produto",
                schema: "estoque",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fornecedor_documento = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    produto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fator = table.Column<int>(type: "integer", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vinculos_de_produto", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "duplicatas_de_nota",
                schema: "estoque",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nota_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    vencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_duplicatas_de_nota", x => x.id);
                    table.ForeignKey(
                        name: "fk_duplicatas_de_nota_notas_nota_id",
                        column: x => x.nota_id,
                        principalSchema: "estoque",
                        principalTable: "notas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "itens_de_nota",
                schema: "estoque",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nota_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ean = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    descricao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ncm = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    unidade = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    quantidade = table.Column<decimal>(type: "numeric(15,4)", precision: 15, scale: 4, nullable: false),
                    valor_total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    custo_total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    fator = table.Column<int>(type: "integer", nullable: false),
                    produto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ignorado = table.Column<bool>(type: "boolean", nullable: false),
                    origem_do_vinculo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_itens_de_nota", x => x.id);
                    table.ForeignKey(
                        name: "fk_itens_de_nota_notas_nota_id",
                        column: x => x.nota_id,
                        principalSchema: "estoque",
                        principalTable: "notas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "distribuicoes_de_item",
                schema: "estoque",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_distribuicoes_de_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_distribuicoes_de_item_itens_de_nota_item_id",
                        column: x => x.item_id,
                        principalSchema: "estoque",
                        principalTable: "itens_de_nota",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_distribuicoes_de_item_item_id",
                schema: "estoque",
                table: "distribuicoes_de_item",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_distribuicoes_de_item_tenant_id",
                schema: "estoque",
                table: "distribuicoes_de_item",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_duplicatas_de_nota_nota_id",
                schema: "estoque",
                table: "duplicatas_de_nota",
                column: "nota_id");

            migrationBuilder.CreateIndex(
                name: "ix_duplicatas_de_nota_tenant_id",
                schema: "estoque",
                table: "duplicatas_de_nota",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_itens_de_nota_nota_id_numero",
                schema: "estoque",
                table: "itens_de_nota",
                columns: new[] { "nota_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_itens_de_nota_tenant_id",
                schema: "estoque",
                table: "itens_de_nota",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_notas_tenant_id",
                schema: "estoque",
                table: "notas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_notas_tenant_id_chave",
                schema: "estoque",
                table: "notas",
                columns: new[] { "tenant_id", "chave" });

            migrationBuilder.CreateIndex(
                name: "ix_notas_tenant_id_empresa_id_importada_em",
                schema: "estoque",
                table: "notas",
                columns: new[] { "tenant_id", "empresa_id", "importada_em" });

            migrationBuilder.CreateIndex(
                name: "ix_vinculos_de_produto_tenant_id",
                schema: "estoque",
                table: "vinculos_de_produto",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_vinculos_de_produto_tenant_id_fornecedor_documento_codigo",
                schema: "estoque",
                table: "vinculos_de_produto",
                columns: new[] { "tenant_id", "fornecedor_documento", "codigo" },
                unique: true);

            foreach (var tabela in new[] { "notas", "itens_de_nota", "distribuicoes_de_item", "duplicatas_de_nota", "vinculos_de_produto" })
                migrationBuilder.EnableTenantRowLevelSecurity("estoque", tabela);
            migrationBuilder.GrantKamusAppRole("estoque");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "distribuicoes_de_item",
                schema: "estoque");

            migrationBuilder.DropTable(
                name: "duplicatas_de_nota",
                schema: "estoque");

            migrationBuilder.DropTable(
                name: "vinculos_de_produto",
                schema: "estoque");

            migrationBuilder.DropTable(
                name: "itens_de_nota",
                schema: "estoque");

            migrationBuilder.DropTable(
                name: "notas",
                schema: "estoque");
        }
    }
}
