using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillingSaaS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFacturaFormaPagoSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FormaPago",
                table: "Facturas",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "01");

            migrationBuilder.AddColumn<decimal>(
                name: "Plazo",
                table: "Facturas",
                type: "decimal(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnidadTiempo",
                table: "Facturas",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FormaPago",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "Plazo",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "UnidadTiempo",
                table: "Facturas");
        }
    }
}
