using System;
using Kamus.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Modules.Financeiro.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "financeiro");

            migrationBuilder.CreateTable(
                name: "categorias",
                schema: "financeiro",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    grupo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ativa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categorias", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contas",
                schema: "financeiro",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    saldo_inicial = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    saldo_inicial_em = table.Column<DateOnly>(type: "date", nullable: false),
                    ativa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "titulos",
                schema: "financeiro",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    descricao = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    pessoa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pessoa_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    documento = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    competencia = table.Column<DateOnly>(type: "date", nullable: false),
                    observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    origem = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    origem_id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    cancelado = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_titulos", x => x.id);
                    table.ForeignKey(
                        name: "fk_titulos_categorias_categoria_id",
                        column: x => x.categoria_id,
                        principalSchema: "financeiro",
                        principalTable: "categorias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "movimentos",
                schema: "financeiro",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    baixa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    titulo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transferencia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usuario = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    registrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movimentos", x => x.id);
                    table.ForeignKey(
                        name: "fk_movimentos_contas_conta_id",
                        column: x => x.conta_id,
                        principalSchema: "financeiro",
                        principalTable: "contas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parcelas",
                schema: "financeiro",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    vencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parcelas", x => x.id);
                    table.ForeignKey(
                        name: "fk_parcelas_titulos_titulo_id",
                        column: x => x.titulo_id,
                        principalSchema: "financeiro",
                        principalTable: "titulos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "baixas",
                schema: "financeiro",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parcela_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    conta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    juros = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    multa = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    desconto = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    usuario = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    registrada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    estornada = table.Column<bool>(type: "boolean", nullable: false),
                    estornada_por = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    estornada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_baixas", x => x.id);
                    table.ForeignKey(
                        name: "fk_baixas_contas_conta_id",
                        column: x => x.conta_id,
                        principalSchema: "financeiro",
                        principalTable: "contas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_baixas_parcelas_parcela_id",
                        column: x => x.parcela_id,
                        principalSchema: "financeiro",
                        principalTable: "parcelas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_baixas_conta_id",
                schema: "financeiro",
                table: "baixas",
                column: "conta_id");

            migrationBuilder.CreateIndex(
                name: "ix_baixas_parcela_id",
                schema: "financeiro",
                table: "baixas",
                column: "parcela_id");

            migrationBuilder.CreateIndex(
                name: "ix_baixas_tenant_id",
                schema: "financeiro",
                table: "baixas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_categorias_tenant_id",
                schema: "financeiro",
                table: "categorias",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_categorias_tenant_id_tipo_nome",
                schema: "financeiro",
                table: "categorias",
                columns: new[] { "tenant_id", "tipo", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contas_tenant_id",
                schema: "financeiro",
                table: "contas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_contas_tenant_id_empresa_id_nome",
                schema: "financeiro",
                table: "contas",
                columns: new[] { "tenant_id", "empresa_id", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_movimentos_conta_id",
                schema: "financeiro",
                table: "movimentos",
                column: "conta_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimentos_tenant_id",
                schema: "financeiro",
                table: "movimentos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimentos_tenant_id_conta_id_data",
                schema: "financeiro",
                table: "movimentos",
                columns: new[] { "tenant_id", "conta_id", "data" });

            migrationBuilder.CreateIndex(
                name: "ix_parcelas_tenant_id",
                schema: "financeiro",
                table: "parcelas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_parcelas_tenant_id_vencimento",
                schema: "financeiro",
                table: "parcelas",
                columns: new[] { "tenant_id", "vencimento" });

            migrationBuilder.CreateIndex(
                name: "ix_parcelas_titulo_id",
                schema: "financeiro",
                table: "parcelas",
                column: "titulo_id");

            migrationBuilder.CreateIndex(
                name: "ix_titulos_categoria_id",
                schema: "financeiro",
                table: "titulos",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "ix_titulos_tenant_id",
                schema: "financeiro",
                table: "titulos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_titulos_tenant_id_empresa_id_tipo",
                schema: "financeiro",
                table: "titulos",
                columns: new[] { "tenant_id", "empresa_id", "tipo" });

            migrationBuilder.CreateIndex(
                name: "ix_titulos_tenant_id_origem_origem_id",
                schema: "financeiro",
                table: "titulos",
                columns: new[] { "tenant_id", "origem", "origem_id" });

            foreach (var tabela in new[] { "contas", "categorias", "titulos", "parcelas", "baixas", "movimentos" })
                migrationBuilder.EnableTenantRowLevelSecurity("financeiro", tabela);
            migrationBuilder.GrantKamusAppRole("financeiro");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "baixas",
                schema: "financeiro");

            migrationBuilder.DropTable(
                name: "movimentos",
                schema: "financeiro");

            migrationBuilder.DropTable(
                name: "parcelas",
                schema: "financeiro");

            migrationBuilder.DropTable(
                name: "contas",
                schema: "financeiro");

            migrationBuilder.DropTable(
                name: "titulos",
                schema: "financeiro");

            migrationBuilder.DropTable(
                name: "categorias",
                schema: "financeiro");
        }
    }
}
