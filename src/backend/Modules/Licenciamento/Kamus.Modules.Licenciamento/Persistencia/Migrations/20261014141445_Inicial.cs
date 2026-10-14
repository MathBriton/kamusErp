using System;
using Kamus.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Modules.Licenciamento.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "licenciamento");

            migrationBuilder.CreateTable(
                name: "acoes_administrativas",
                schema: "licenciamento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ocorrido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    administrador_id = table.Column<Guid>(type: "uuid", nullable: true),
                    administrador_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    acao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acoes_administrativas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "administradores",
                schema: "licenciamento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    senha_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    falhas_de_login = table.Column<int>(type: "integer", nullable: false),
                    bloqueado_ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_login_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_administradores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "licencas",
                schema: "licenciamento",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plano = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    valor_mensal = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    dias_de_carencia = table.Column<int>(type: "integer", nullable: false),
                    trial_ate = table.Column<DateOnly>(type: "date", nullable: true),
                    pago_ate = table.Column<DateOnly>(type: "date", nullable: true),
                    cancelada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    limite_de_cnpjs = table.Column<int>(type: "integer", nullable: true),
                    limite_de_lojas = table.Column<int>(type: "integer", nullable: true),
                    limite_de_usuarios = table.Column<int>(type: "integer", nullable: true),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ultimo_aviso = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_licencas", x => x.tenant_id);
                });

            migrationBuilder.CreateTable(
                name: "pagamentos",
                schema: "licenciamento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    meses = table.Column<int>(type: "integer", nullable: false),
                    pago_em = table.Column<DateOnly>(type: "date", nullable: false),
                    estendeu_ate = table.Column<DateOnly>(type: "date", nullable: false),
                    forma = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    registrado_por = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    registrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pagamentos", x => x.id);
                    table.ForeignKey(
                        name: "fk_pagamentos_licencas_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "licenciamento",
                        principalTable: "licencas",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_acoes_administrativas_ocorrido_em",
                schema: "licenciamento",
                table: "acoes_administrativas",
                column: "ocorrido_em");

            migrationBuilder.CreateIndex(
                name: "ix_acoes_administrativas_tenant_id_ocorrido_em",
                schema: "licenciamento",
                table: "acoes_administrativas",
                columns: new[] { "tenant_id", "ocorrido_em" });

            migrationBuilder.CreateIndex(
                name: "ix_administradores_email",
                schema: "licenciamento",
                table: "administradores",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_tenant_id_pago_em",
                schema: "licenciamento",
                table: "pagamentos",
                columns: new[] { "tenant_id", "pago_em" });

            // Sem RLS de propósito: dados do operador do SaaS (o Painel Administrativo vê todos os clientes).
            migrationBuilder.GrantKamusAppRole("licenciamento");
            // Registro das ações administrativas: só inclusão.
            migrationBuilder.SomenteInclusaoParaKamusApp("licenciamento", "acoes_administrativas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "acoes_administrativas",
                schema: "licenciamento");

            migrationBuilder.DropTable(
                name: "administradores",
                schema: "licenciamento");

            migrationBuilder.DropTable(
                name: "pagamentos",
                schema: "licenciamento");

            migrationBuilder.DropTable(
                name: "licencas",
                schema: "licenciamento");
        }
    }
}
