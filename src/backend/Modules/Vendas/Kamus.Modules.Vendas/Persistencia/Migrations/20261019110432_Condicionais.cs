using System;
using Kamus.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Modules.Vendas.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Condicionais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "limite_condicional_pecas",
                schema: "vendas",
                table: "configuracoes",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<decimal>(
                name: "limite_condicional_valor",
                schema: "vendas",
                table: "configuracoes",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 1000m);

            migrationBuilder.AddColumn<int>(
                name: "prazo_condicional_dias",
                schema: "vendas",
                table: "configuracoes",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.CreateTable(
                name: "condicionais",
                schema: "vendas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    loja_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    prazo = table.Column<DateOnly>(type: "date", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    vendedor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vendedor_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    operacao_de_saida_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fechada = table.Column<bool>(type: "boolean", nullable: false),
                    fechada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fechada_por = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    venda_id = table.Column<Guid>(type: "uuid", nullable: true),
                    venda_numero = table.Column<int>(type: "integer", nullable: true),
                    registrada_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    registrada_por = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    registrada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_condicionais", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "itens_da_condicional",
                schema: "vendas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    condicional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    produto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    preco_unitario = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    custo_unitario = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    devolvidas = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_itens_da_condicional", x => x.id);
                    table.ForeignKey(
                        name: "fk_itens_da_condicional_condicionais_condicional_id",
                        column: x => x.condicional_id,
                        principalSchema: "vendas",
                        principalTable: "condicionais",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_condicionais_tenant_id",
                schema: "vendas",
                table: "condicionais",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_condicionais_tenant_id_cliente_id_fechada",
                schema: "vendas",
                table: "condicionais",
                columns: new[] { "tenant_id", "cliente_id", "fechada" });

            migrationBuilder.CreateIndex(
                name: "ix_condicionais_tenant_id_empresa_id_numero",
                schema: "vendas",
                table: "condicionais",
                columns: new[] { "tenant_id", "empresa_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_itens_da_condicional_condicional_id",
                schema: "vendas",
                table: "itens_da_condicional",
                column: "condicional_id");

            migrationBuilder.CreateIndex(
                name: "ix_itens_da_condicional_tenant_id",
                schema: "vendas",
                table: "itens_da_condicional",
                column: "tenant_id");

            foreach (var tabela in new[] { "condicionais", "itens_da_condicional" })
                migrationBuilder.EnableTenantRowLevelSecurity("vendas", tabela);
            migrationBuilder.GrantKamusAppRole("vendas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "itens_da_condicional",
                schema: "vendas");

            migrationBuilder.DropTable(
                name: "condicionais",
                schema: "vendas");

            migrationBuilder.DropColumn(
                name: "limite_condicional_pecas",
                schema: "vendas",
                table: "configuracoes");

            migrationBuilder.DropColumn(
                name: "limite_condicional_valor",
                schema: "vendas",
                table: "configuracoes");

            migrationBuilder.DropColumn(
                name: "prazo_condicional_dias",
                schema: "vendas",
                table: "configuracoes");
        }
    }
}
