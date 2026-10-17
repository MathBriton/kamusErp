using System;
using Kamus.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Modules.Vendas.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "vendas");

            migrationBuilder.CreateTable(
                name: "configuracoes",
                schema: "vendas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permitir_estoque_negativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracoes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "formas_de_pagamento",
                schema: "vendas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    conta_id = table.Column<Guid>(type: "uuid", nullable: true),
                    taxa_a_vista = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    taxa_parcelado = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    max_parcelas = table.Column<int>(type: "integer", nullable: false),
                    prazo_dias = table.Column<int>(type: "integer", nullable: false),
                    ativa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_formas_de_pagamento", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vendas",
                schema: "vendas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    loja_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cliente_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    vendedor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vendedor_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    comissao_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    desconto_geral = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    troco = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    nota_externa = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    registrada_por = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    registrada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    operacao_de_estoque_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo_do_cancelamento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cancelada_por = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    cancelada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vendas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "itens_da_venda",
                schema: "vendas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venda_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    produto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    preco_unitario = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    desconto = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    custo_unitario = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_itens_da_venda", x => x.id);
                    table.ForeignKey(
                        name: "fk_itens_da_venda_vendas_venda_id",
                        column: x => x.venda_id,
                        principalSchema: "vendas",
                        principalTable: "vendas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pagamentos_da_venda",
                schema: "vendas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venda_id = table.Column<Guid>(type: "uuid", nullable: false),
                    forma_id = table.Column<Guid>(type: "uuid", nullable: false),
                    forma_nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    parcelas = table.Column<int>(type: "integer", nullable: false),
                    taxa_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pagamentos_da_venda", x => x.id);
                    table.ForeignKey(
                        name: "fk_pagamentos_da_venda_vendas_venda_id",
                        column: x => x.venda_id,
                        principalSchema: "vendas",
                        principalTable: "vendas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_configuracoes_tenant_id",
                schema: "vendas",
                table: "configuracoes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_configuracoes_tenant_id_empresa_id",
                schema: "vendas",
                table: "configuracoes",
                columns: new[] { "tenant_id", "empresa_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_formas_de_pagamento_tenant_id",
                schema: "vendas",
                table: "formas_de_pagamento",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_formas_de_pagamento_tenant_id_empresa_id_nome",
                schema: "vendas",
                table: "formas_de_pagamento",
                columns: new[] { "tenant_id", "empresa_id", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_itens_da_venda_tenant_id",
                schema: "vendas",
                table: "itens_da_venda",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_itens_da_venda_tenant_id_sku_id",
                schema: "vendas",
                table: "itens_da_venda",
                columns: new[] { "tenant_id", "sku_id" });

            migrationBuilder.CreateIndex(
                name: "ix_itens_da_venda_venda_id",
                schema: "vendas",
                table: "itens_da_venda",
                column: "venda_id");

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_da_venda_tenant_id",
                schema: "vendas",
                table: "pagamentos_da_venda",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_da_venda_venda_id",
                schema: "vendas",
                table: "pagamentos_da_venda",
                column: "venda_id");

            migrationBuilder.CreateIndex(
                name: "ix_vendas_tenant_id",
                schema: "vendas",
                table: "vendas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_vendas_tenant_id_empresa_id_data",
                schema: "vendas",
                table: "vendas",
                columns: new[] { "tenant_id", "empresa_id", "data" });

            migrationBuilder.CreateIndex(
                name: "ix_vendas_tenant_id_empresa_id_numero",
                schema: "vendas",
                table: "vendas",
                columns: new[] { "tenant_id", "empresa_id", "numero" },
                unique: true);

            foreach (var tabela in new[] { "formas_de_pagamento", "configuracoes", "vendas", "itens_da_venda", "pagamentos_da_venda" })
                migrationBuilder.EnableTenantRowLevelSecurity("vendas", tabela);
            migrationBuilder.GrantKamusAppRole("vendas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracoes",
                schema: "vendas");

            migrationBuilder.DropTable(
                name: "formas_de_pagamento",
                schema: "vendas");

            migrationBuilder.DropTable(
                name: "itens_da_venda",
                schema: "vendas");

            migrationBuilder.DropTable(
                name: "pagamentos_da_venda",
                schema: "vendas");

            migrationBuilder.DropTable(
                name: "vendas",
                schema: "vendas");
        }
    }
}
