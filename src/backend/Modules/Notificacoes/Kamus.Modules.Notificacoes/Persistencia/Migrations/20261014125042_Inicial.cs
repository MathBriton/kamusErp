using System;
using Kamus.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Modules.Notificacoes.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notificacoes");

            migrationBuilder.CreateTable(
                name: "notificacoes",
                schema: "notificacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    titulo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    mensagem = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    link = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notificacoes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notificacoes_tenant_id",
                schema: "notificacoes",
                table: "notificacoes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_notificacoes_usuario_id_lida_em_criada_em",
                schema: "notificacoes",
                table: "notificacoes",
                columns: new[] { "usuario_id", "lida_em", "criada_em" });

            migrationBuilder.EnableTenantRowLevelSecurity("notificacoes", "notificacoes");
            migrationBuilder.GrantKamusAppRole("notificacoes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notificacoes",
                schema: "notificacoes");
        }
    }
}
