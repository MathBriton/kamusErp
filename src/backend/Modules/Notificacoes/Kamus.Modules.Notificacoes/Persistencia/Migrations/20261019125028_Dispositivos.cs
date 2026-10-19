using System;
using Kamus.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Modules.Notificacoes.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Dispositivos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dispositivos",
                schema: "notificacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    plataforma = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    registrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dispositivos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dispositivos_tenant_id",
                schema: "notificacoes",
                table: "dispositivos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispositivos_tenant_id_token",
                schema: "notificacoes",
                table: "dispositivos",
                columns: new[] { "tenant_id", "token" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dispositivos_usuario_id",
                schema: "notificacoes",
                table: "dispositivos",
                column: "usuario_id");

            migrationBuilder.EnableTenantRowLevelSecurity("notificacoes", "dispositivos");
            migrationBuilder.GrantKamusAppRole("notificacoes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dispositivos",
                schema: "notificacoes");
        }
    }
}
