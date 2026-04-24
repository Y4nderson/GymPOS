using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymPOS.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTurnoId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TurnoId",
                table: "Ventas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TurnoId",
                table: "MovimientosCaja",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TurnoId",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "TurnoId",
                table: "MovimientosCaja");
        }
    }
}
