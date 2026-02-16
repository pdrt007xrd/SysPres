using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SysPres.Migrations
{
    /// <inheritdoc />
    public partial class AddPagoTipoYDesgloseCapitalInteres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CapitalAbonado",
                table: "Pagos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InteresAbonado",
                table: "Pagos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TipoPago",
                table: "Pagos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CapitalAbonado",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "InteresAbonado",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "TipoPago",
                table: "Pagos");
        }
    }
}
