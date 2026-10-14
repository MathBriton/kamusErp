using System;
using Kamus.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Modules.Cadastros.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Produtos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cores",
                schema: "cadastros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    hex = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "grades",
                schema: "cadastros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tamanhos = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grades", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "produtos",
                schema: "cadastros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referencia = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    descricao = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    marca = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    categoria = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    colecao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    referencia_do_fornecedor = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    fornecedor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    grade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    preco_venda = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    preco_custo = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_produtos", x => x.id);
                    table.ForeignKey(
                        name: "fk_produtos_grades_grade_id",
                        column: x => x.grade_id,
                        principalSchema: "cadastros",
                        principalTable: "grades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_produtos_pessoas_fornecedor_id",
                        column: x => x.fornecedor_id,
                        principalSchema: "cadastros",
                        principalTable: "pessoas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "skus",
                schema: "cadastros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    produto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tamanho = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ordem_tamanho = table.Column<int>(type: "integer", nullable: false),
                    codigo_de_barras = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    preco_venda = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skus", x => x.id);
                    table.ForeignKey(
                        name: "fk_skus_cores_cor_id",
                        column: x => x.cor_id,
                        principalSchema: "cadastros",
                        principalTable: "cores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_skus_produtos_produto_id",
                        column: x => x.produto_id,
                        principalSchema: "cadastros",
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cores_tenant_id",
                schema: "cadastros",
                table: "cores",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_cores_tenant_id_nome",
                schema: "cadastros",
                table: "cores",
                columns: new[] { "tenant_id", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_grades_tenant_id",
                schema: "cadastros",
                table: "grades",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_grades_tenant_id_nome",
                schema: "cadastros",
                table: "grades",
                columns: new[] { "tenant_id", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_produtos_fornecedor_id",
                schema: "cadastros",
                table: "produtos",
                column: "fornecedor_id");

            migrationBuilder.CreateIndex(
                name: "ix_produtos_grade_id",
                schema: "cadastros",
                table: "produtos",
                column: "grade_id");

            migrationBuilder.CreateIndex(
                name: "ix_produtos_tenant_id",
                schema: "cadastros",
                table: "produtos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_produtos_tenant_id_referencia",
                schema: "cadastros",
                table: "produtos",
                columns: new[] { "tenant_id", "referencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_skus_cor_id",
                schema: "cadastros",
                table: "skus",
                column: "cor_id");

            migrationBuilder.CreateIndex(
                name: "ix_skus_produto_id_cor_id_tamanho",
                schema: "cadastros",
                table: "skus",
                columns: new[] { "produto_id", "cor_id", "tamanho" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_skus_tenant_id",
                schema: "cadastros",
                table: "skus",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_skus_tenant_id_codigo_de_barras",
                schema: "cadastros",
                table: "skus",
                columns: new[] { "tenant_id", "codigo_de_barras" },
                unique: true);

            foreach (var tabela in new[] { "grades", "cores", "produtos", "skus" })
                migrationBuilder.EnableTenantRowLevelSecurity("cadastros", tabela);
            migrationBuilder.GrantKamusAppRole("cadastros");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "skus",
                schema: "cadastros");

            migrationBuilder.DropTable(
                name: "cores",
                schema: "cadastros");

            migrationBuilder.DropTable(
                name: "produtos",
                schema: "cadastros");

            migrationBuilder.DropTable(
                name: "grades",
                schema: "cadastros");
        }
    }
}
