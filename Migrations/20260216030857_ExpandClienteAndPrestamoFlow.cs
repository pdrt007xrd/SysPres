using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SysPres.Migrations
{
    /// <inheritdoc />
    public partial class ExpandClienteAndPrestamoFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PlazoMeses",
                table: "Prestamos",
                newName: "NumeroPagos");

            migrationBuilder.AddColumn<string>(
                name: "FrecuenciaPago",
                table: "Prestamos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "MontoInteres",
                table: "Prestamos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SaldoPendiente",
                table: "Prestamos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAPagar",
                table: "Prestamos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorCuota",
                table: "Prestamos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Empresa",
                table: "Clientes",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GaranteDireccion",
                table: "Clientes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GaranteDocumento",
                table: "Clientes",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GaranteNombre",
                table: "Clientes",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GaranteTelefono",
                table: "Clientes",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IngresoMensual",
                table: "Clientes",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MesesLaborando",
                table: "Clientes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Puesto",
                table: "Clientes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TieneGarante",
                table: "Clientes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PrestamoCuotas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrestamoId = table.Column<int>(type: "int", nullable: false),
                    NumeroCuota = table.Column<int>(type: "int", nullable: false),
                    FechaVencimiento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MontoCuota = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaPago = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrestamoCuotas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrestamoCuotas_Prestamos_PrestamoId",
                        column: x => x.PrestamoId,
                        principalTable: "Prestamos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrestamoCuotas_PrestamoId",
                table: "PrestamoCuotas",
                column: "PrestamoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrestamoCuotas");

            migrationBuilder.DropColumn(
                name: "FrecuenciaPago",
                table: "Prestamos");

            migrationBuilder.DropColumn(
                name: "MontoInteres",
                table: "Prestamos");

            migrationBuilder.DropColumn(
                name: "SaldoPendiente",
                table: "Prestamos");

            migrationBuilder.DropColumn(
                name: "TotalAPagar",
                table: "Prestamos");

            migrationBuilder.DropColumn(
                name: "ValorCuota",
                table: "Prestamos");

            migrationBuilder.DropColumn(
                name: "Empresa",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "GaranteDireccion",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "GaranteDocumento",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "GaranteNombre",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "GaranteTelefono",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "IngresoMensual",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "MesesLaborando",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Puesto",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "TieneGarante",
                table: "Clientes");

            migrationBuilder.RenameColumn(
                name: "NumeroPagos",
                table: "Prestamos",
                newName: "PlazoMeses");
        }
    }
}
