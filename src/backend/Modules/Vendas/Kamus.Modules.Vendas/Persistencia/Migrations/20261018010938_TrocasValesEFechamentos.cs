using System;
using Kamus.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Modules.Vendas.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class TrocasValesEFechamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "fechamento_id",
                schema: "vendas",
                table: "vendas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "vale_id",
                schema: "vendas",
                table: "pagamentos_da_venda",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "prazo_de_troca_dias",
                schema: "vendas",
                table: "configuracoes",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "validade_do_vale_dias",
                schema: "vendas",
                table: "configuracoes",
                type: "integer",
                nullable: false,
                defaultValue: 90);

            migrationBuilder.CreateTable(
                name: "fechamentos_de_comissao",
                schema: "vendas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vendedor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vendedor_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    de = table.Column<DateOnly>(type: "date", nullable: false),
                    ate = table.Column<DateOnly>(type: "date", nullable: false),
                    vendas = table.Column<int>(type: "integer", nullable: false),
                    trocas = table.Column<int>(type: "integer", nullable: false),
                    total_vendido = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    comissao = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    fechado_por = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    fechado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    conta_a_pagar_gerada = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fechamentos_de_comissao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trocas",
                schema: "vendas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    loja_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    venda_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venda_numero = table.Column<int>(type: "integer", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cliente_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    vendedor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vendedor_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    comissao_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    motivo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    troco = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    vale_id = table.Column<Guid>(type: "uuid", nullable: true),
                    operacao_de_devolucao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    operacao_de_saida_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fechamento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    registrada_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    registrada_por = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    registrada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trocas", x => x.id);
                    table.ForeignKey(
                        name: "fk_trocas_vendas_venda_id",
                        column: x => x.venda_id,
                        principalSchema: "vendas",
                        principalTable: "vendas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vales_troca",
                schema: "vendas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cliente_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    saldo = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    emitido_em = table.Column<DateOnly>(type: "date", nullable: false),
                    valido_ate = table.Column<DateOnly>(type: "date", nullable: false),
                    troca_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelado = table.Column<bool>(type: "boolean", nullable: false),
                    motivo_do_cancelamento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cancelado_por = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vales_troca", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "itens_devolvidos",
                schema: "vendas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    troca_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    produto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    custo_unitario = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                    destino = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_itens_devolvidos", x => x.id);
                    table.ForeignKey(
                        name: "fk_itens_devolvidos_trocas_troca_id",
                        column: x => x.troca_id,
                        principalSchema: "vendas",
                        principalTable: "trocas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "itens_levados",
                schema: "vendas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    troca_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_itens_levados", x => x.id);
                    table.ForeignKey(
                        name: "fk_itens_levados_trocas_troca_id",
                        column: x => x.troca_id,
                        principalSchema: "vendas",
                        principalTable: "trocas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pagamentos_da_troca",
                schema: "vendas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    troca_id = table.Column<Guid>(type: "uuid", nullable: false),
                    forma_id = table.Column<Guid>(type: "uuid", nullable: false),
                    forma_nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    parcelas = table.Column<int>(type: "integer", nullable: false),
                    taxa_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    vale_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pagamentos_da_troca", x => x.id);
                    table.ForeignKey(
                        name: "fk_pagamentos_da_troca_trocas_troca_id",
                        column: x => x.troca_id,
                        principalSchema: "vendas",
                        principalTable: "trocas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vendas_tenant_id_cliente_id",
                schema: "vendas",
                table: "vendas",
                columns: new[] { "tenant_id", "cliente_id" });

            migrationBuilder.CreateIndex(
                name: "ix_fechamentos_de_comissao_tenant_id",
                schema: "vendas",
                table: "fechamentos_de_comissao",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_fechamentos_de_comissao_tenant_id_empresa_id_vendedor_id",
                schema: "vendas",
                table: "fechamentos_de_comissao",
                columns: new[] { "tenant_id", "empresa_id", "vendedor_id" });

            migrationBuilder.CreateIndex(
                name: "ix_itens_devolvidos_tenant_id",
                schema: "vendas",
                table: "itens_devolvidos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_itens_devolvidos_troca_id",
                schema: "vendas",
                table: "itens_devolvidos",
                column: "troca_id");

            migrationBuilder.CreateIndex(
                name: "ix_itens_levados_tenant_id",
                schema: "vendas",
                table: "itens_levados",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_itens_levados_troca_id",
                schema: "vendas",
                table: "itens_levados",
                column: "troca_id");

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_da_troca_tenant_id",
                schema: "vendas",
                table: "pagamentos_da_troca",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_da_troca_troca_id",
                schema: "vendas",
                table: "pagamentos_da_troca",
                column: "troca_id");

            migrationBuilder.CreateIndex(
                name: "ix_trocas_tenant_id",
                schema: "vendas",
                table: "trocas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_trocas_tenant_id_empresa_id_numero",
                schema: "vendas",
                table: "trocas",
                columns: new[] { "tenant_id", "empresa_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trocas_tenant_id_venda_id",
                schema: "vendas",
                table: "trocas",
                columns: new[] { "tenant_id", "venda_id" });

            migrationBuilder.CreateIndex(
                name: "ix_trocas_venda_id",
                schema: "vendas",
                table: "trocas",
                column: "venda_id");

            migrationBuilder.CreateIndex(
                name: "ix_vales_troca_tenant_id",
                schema: "vendas",
                table: "vales_troca",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_vales_troca_tenant_id_cliente_id",
                schema: "vendas",
                table: "vales_troca",
                columns: new[] { "tenant_id", "cliente_id" });

            migrationBuilder.CreateIndex(
                name: "ix_vales_troca_tenant_id_codigo",
                schema: "vendas",
                table: "vales_troca",
                columns: new[] { "tenant_id", "codigo" },
                unique: true);

            foreach (var tabela in new[] { "trocas", "itens_devolvidos", "itens_levados", "pagamentos_da_troca", "vales_troca", "fechamentos_de_comissao" })
                migrationBuilder.EnableTenantRowLevelSecurity("vendas", tabela);
            migrationBuilder.GrantKamusAppRole("vendas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fechamentos_de_comissao",
                schema: "vendas");

            migrationBuilder.DropTable(
                name: "itens_devolvidos",
                schema: "vendas");

            migrationBuilder.DropTable(
                name: "itens_levados",
                schema: "vendas");

            migrationBuilder.DropTable(
                name: "pagamentos_da_troca",
                schema: "vendas");

            migrationBuilder.DropTable(
                name: "vales_troca",
                schema: "vendas");

            migrationBuilder.DropTable(
                name: "trocas",
                schema: "vendas");

            migrationBuilder.DropIndex(
                name: "ix_vendas_tenant_id_cliente_id",
                schema: "vendas",
                table: "vendas");

            migrationBuilder.DropColumn(
                name: "fechamento_id",
                schema: "vendas",
                table: "vendas");

            migrationBuilder.DropColumn(
                name: "vale_id",
                schema: "vendas",
                table: "pagamentos_da_venda");

            migrationBuilder.DropColumn(
                name: "prazo_de_troca_dias",
                schema: "vendas",
                table: "configuracoes");

            migrationBuilder.DropColumn(
                name: "validade_do_vale_dias",
                schema: "vendas",
                table: "configuracoes");
        }
    }
}
