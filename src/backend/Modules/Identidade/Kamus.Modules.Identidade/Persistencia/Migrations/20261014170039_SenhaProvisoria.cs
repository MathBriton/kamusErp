using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Modules.Identidade.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class SenhaProvisoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "deve_trocar_senha",
                schema: "identidade",
                table: "usuarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deve_trocar_senha",
                schema: "identidade",
                table: "usuarios");
        }
    }
}
