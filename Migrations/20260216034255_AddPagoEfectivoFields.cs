using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SysPres.Migrations
{
    /// <inheritdoc />
    public partial class AddPagoEfectivoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CambioDevuelto",
                table: "Pagos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "MetodoPago",
                table: "Pagos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "MontoRecibido",
                table: "Pagos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CambioDevuelto",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "MetodoPago",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "MontoRecibido",
                table: "Pagos");
        }
    }
}
