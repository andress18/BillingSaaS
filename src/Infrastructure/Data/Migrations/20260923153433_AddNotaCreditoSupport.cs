using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillingSaaS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotaCreditoSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SecuencialNotaCredito",
                table: "Emisores",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "NotasCredito",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmisorId = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NumeroAutorizacion = table.Column<string>(type: "nvarchar(49)", maxLength: 49, nullable: true),
                    FechaAutorizacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MensajeErrorSri = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    XmlFirmado = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ambiente = table.Column<int>(type: "int", nullable: false),
                    TipoEmision = table.Column<int>(type: "int", nullable: false),
                    RazonSocial = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Ruc = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    ClaveAcceso = table.Column<string>(type: "nvarchar(49)", maxLength: 49, nullable: false),
                    CodDoc = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Establecimiento = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PuntoEmision = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Secuencial = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    DireccionMatriz = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ContribuyenteRimpe = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    CodDocModificado = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    NumDocModificado = table.Column<string>(type: "nvarchar(17)", maxLength: 17, nullable: false),
                    FechaEmisionDocSustento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FechaEmision = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TipoIdentificacionComprador = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    RazonSocialComprador = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IdentificacionComprador = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalSinImpuestos = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDescuento = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorModificacion = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    CatalogoClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasCredito", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotasCredito_Comprador_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Comprador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasCredito_Emisores_EmisorId",
                        column: x => x.EmisorId,
                        principalTable: "Emisores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DetallesNotaCredito",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotaCreditoId = table.Column<int>(type: "int", nullable: false),
                    CodigoPrincipal = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Descuento = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PrecioTotalSinImpuesto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CatalogoProductoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Impuestos = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesNotaCredito", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetallesNotaCredito_NotasCredito_NotaCreditoId",
                        column: x => x.NotaCreditoId,
                        principalTable: "NotasCredito",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesNotaCredito_NotaCreditoId",
                table: "DetallesNotaCredito",
                column: "NotaCreditoId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCredito_ClaveAcceso",
                table: "NotasCredito",
                column: "ClaveAcceso");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCredito_ClienteId",
                table: "NotasCredito",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCredito_EmisorId",
                table: "NotasCredito",
                column: "EmisorId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCredito_TenantId",
                table: "NotasCredito",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetallesNotaCredito");

            migrationBuilder.DropTable(
                name: "NotasCredito");

            migrationBuilder.DropColumn(
                name: "SecuencialNotaCredito",
                table: "Emisores");
        }
    }
}
