using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SysPres.Migrations
{
    /// <inheritdoc />
    public partial class AddPagoBalancePendiente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BalancePendiente",
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
                name: "BalancePendiente",
                table: "Pagos");
        }
    }
}
