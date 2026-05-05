using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymPOS.Migrations
{
    /// <inheritdoc />
    public partial class CambiarEmailPorUsername : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Email",
                table: "Usuarios",
                newName: "Username");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Username",
                table: "Usuarios",
                newName: "Email");
        }
    }
}
