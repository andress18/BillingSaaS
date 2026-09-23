using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillingSaaS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotasDebito : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CatalogoClienteId",
                table: "Facturas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CatalogoProductoId",
                table: "DetalleFactura",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CatalogoClientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoIdentificacion = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Identificacion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RazonSocial = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Direccion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CorreoElectronico = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogoClientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogoProductos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodigoPrincipal = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CodigoImpuesto = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CodigoPorcentaje = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Tarifa = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogoProductos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogoClientes_TenantId_Identificacion",
                table: "CatalogoClientes",
                columns: new[] { "TenantId", "Identificacion" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogoClientes_TenantId_RazonSocial",
                table: "CatalogoClientes",
                columns: new[] { "TenantId", "RazonSocial" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogoProductos_TenantId_CodigoPrincipal",
                table: "CatalogoProductos",
                columns: new[] { "TenantId", "CodigoPrincipal" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogoProductos_TenantId_Descripcion",
                table: "CatalogoProductos",
                columns: new[] { "TenantId", "Descripcion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogoClientes");

            migrationBuilder.DropTable(
                name: "CatalogoProductos");

            migrationBuilder.DropColumn(
                name: "CatalogoClienteId",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "CatalogoProductoId",
                table: "DetalleFactura");
        }
    }
}
