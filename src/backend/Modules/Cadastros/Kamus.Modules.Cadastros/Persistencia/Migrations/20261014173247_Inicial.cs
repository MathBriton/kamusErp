using System;
using Kamus.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Modules.Cadastros.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cadastros");

            migrationBuilder.CreateTable(
                name: "pessoas",
                schema: "cadastros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    nome_fantasia = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    documento = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    eh_cliente = table.Column<bool>(type: "boolean", nullable: false),
                    eh_fornecedor = table.Column<bool>(type: "boolean", nullable: false),
                    telefone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    data_nascimento = table.Column<DateOnly>(type: "date", nullable: true),
                    endereco_cep = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    endereco_logradouro = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    endereco_numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    endereco_complemento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    endereco_bairro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    endereco_cidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    endereco_uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    preferencia_blusa = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    preferencia_calca = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    preferencia_calcado = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    preferencia_estilo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    consentimento_marketing_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    anonimizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pessoas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vendedores",
                schema: "cadastros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    comissao_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    telefone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vendedores", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pessoas_tenant_id",
                schema: "cadastros",
                table: "pessoas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_pessoas_tenant_id_documento",
                schema: "cadastros",
                table: "pessoas",
                columns: new[] { "tenant_id", "documento" },
                unique: true,
                filter: "documento IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_pessoas_tenant_id_nome",
                schema: "cadastros",
                table: "pessoas",
                columns: new[] { "tenant_id", "nome" });

            migrationBuilder.CreateIndex(
                name: "ix_vendedores_tenant_id",
                schema: "cadastros",
                table: "vendedores",
                column: "tenant_id");

            migrationBuilder.EnableTenantRowLevelSecurity("cadastros", "pessoas");
            migrationBuilder.EnableTenantRowLevelSecurity("cadastros", "vendedores");
            migrationBuilder.GrantKamusAppRole("cadastros");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pessoas",
                schema: "cadastros");

            migrationBuilder.DropTable(
                name: "vendedores",
                schema: "cadastros");
        }
    }
}
