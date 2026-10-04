using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillingSaaS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantEstadoYActivacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "Tenants",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "PENDIENTE_PAGO");

            migrationBuilder.Sql("UPDATE Tenants SET Estado = 'ACTIVO';");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaActivacion",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TokenActivacion",
                table: "Tenants",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_TokenActivacion",
                table: "Tenants",
                column: "TokenActivacion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_TokenActivacion",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "FechaActivacion",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TokenActivacion",
                table: "Tenants");
        }
    }
}
