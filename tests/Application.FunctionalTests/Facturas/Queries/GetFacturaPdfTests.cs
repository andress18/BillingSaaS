using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using BillingSaaS.Application.Facturas.Queries.GetFacturaPdf;
using BillingSaaS.Domain.Entities;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.FunctionalTests.Facturas.Queries;

public class GetFacturaPdfTests : TestBase
{
    [Test]
    public async Task GetFacturaPdf_FacturaExistente_DebeRetornarPdfValido()
    {
        // Arrange
        await TestApp.RunAsDefaultUserAsync();
        var tenantId = TestApp.GetTenantId()!.Value;

        var emisor = Emisor.Crear(
            tenantId: tenantId,
            ruc: "0957790108001",
            razonSocial: "EMPRESA PRUEBA S.A.",
            direccionMatriz: "Av. Principal 100",
            obligadoContabilidad: true,
            regimenRimpe: "CONTRIBUYENTE RÉGIMEN RIMPE"
        );
        await TestApp.AddAsync(emisor);

        var comprador = Comprador.Crear("04", "1792060346001", "CLIENTE EMPRESARIAL SA", "Quito", "contacto@empresa.com");
        var detalle = DetalleFactura.Crear("SERV-01", "Consultoria TI", 1, 100m, 0, [Impuesto.Crear("2", "4", 15m, 100m)]);

        var factura = Factura.Crear(
            tenantId, 1, emisor.RazonSocial, emisor.Ruc,
            "001", "001", "000000101", emisor.DireccionMatriz,
            DateTime.UtcNow, comprador, [detalle], emisor.Id);

        string claveAcceso = "1609202601095779010800110010010000001011234567813";
        factura.AsignarClaveAcceso(claveAcceso);
        factura.MarcarComoAutorizada(claveAcceso, DateTime.UtcNow);

        await TestApp.AddAsync(factura);

        // Act
        var query = new GetFacturaPdfQuery(factura.Id);
        var result = await TestApp.SendAsync(query);

        // Assert
        result.ShouldNotBeNull();
        result.ContentType.ShouldBe("application/pdf");
        result.FileName.ShouldBe("FACTURA_001_001_000000101.pdf");
        result.Content.ShouldNotBeNull();
        result.Content.Length.ShouldBeGreaterThan(1000);

        string magicBytes = Encoding.ASCII.GetString(result.Content, 0, 4);
        magicBytes.ShouldBe("%PDF");
    }

    [Test]
    public async Task GetFacturaPdf_FacturaDeOtroTenant_DebeLanzarUnauthorizedAccessException()
    {
        // Arrange
        await TestApp.RunAsDefaultUserAsync();

        var otroTenantId = Guid.NewGuid();
        var otroEmisor = Emisor.Crear(otroTenantId, "0957790108002", "OTRA EMPRESA", "Guayaquil");
        await TestApp.AddAsync(otroEmisor);

        var comprador = Comprador.Crear("07", "9999999999999", "CONSUMIDOR FINAL");
        var detalle = DetalleFactura.Crear("SERV-01", "Servicio General", 1, 10m, 0, [Impuesto.Crear("2", "0", 0m, 10m)]);

        var factura = Factura.Crear(
            otroTenantId, 1, otroEmisor.RazonSocial, otroEmisor.Ruc,
            "001", "001", "000000999", otroEmisor.DireccionMatriz,
            DateTime.UtcNow, comprador, [detalle], otroEmisor.Id);
        factura.AsignarClaveAcceso("1609202601095779010800210010010000009991234567813");

        await TestApp.AddAsync(factura);

        // Act & Assert
        var query = new GetFacturaPdfQuery(factura.Id);
        await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
        {
            await TestApp.SendAsync(query);
        });
    }

    [Test]
    public async Task GetFacturaPdf_FacturaInexistente_DebeLanzarNotFoundException()
    {
        await TestApp.RunAsDefaultUserAsync();

        var query = new GetFacturaPdfQuery(999999);

        await Should.ThrowAsync<NotFoundException>(async () =>
        {
            await TestApp.SendAsync(query);
        });
    }
}
