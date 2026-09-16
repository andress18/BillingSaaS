using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using BillingSaaS.Application.Facturas.Queries.GetFacturaXml;
using BillingSaaS.Domain.Entities;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.FunctionalTests.Facturas.Queries;

public class GetFacturaXmlTests : TestBase
{
    [Test]
    public async Task GetFacturaXml_FacturaExistenteAutorizada_DebeRetornarXmlConAutorizacion()
    {
        // Arrange
        await TestApp.RunAsDefaultUserAsync();
        var tenantId = TestApp.GetTenantId()!.Value;

        var emisor = Emisor.Crear(tenantId, "0957790108001", "EMPRESA PRUEBA S.A.", "Av. Principal 100");
        await TestApp.AddAsync(emisor);

        var comprador = Comprador.Crear("04", "1792060346001", "CLIENTE EMPRESARIAL SA", "Quito", "contacto@empresa.com");
        var detalle = DetalleFactura.Crear("SERV-01", "Consultoria TI", 1, 100m, 0, [Impuesto.Crear("2", "4", 15m, 100m)]);

        var factura = Factura.Crear(
            tenantId, 1, emisor.RazonSocial, emisor.Ruc,
            "001", "001", "000000201", emisor.DireccionMatriz,
            DateTime.UtcNow, comprador, [detalle], emisor.Id);

        string claveAcceso = "1609202601095779010800110010010000002011234567813";
        factura.AsignarClaveAcceso(claveAcceso);
        factura.MarcarComoAutorizada(claveAcceso, DateTime.UtcNow);

        await TestApp.AddAsync(factura);

        // Act
        var query = new GetFacturaXmlQuery(factura.Id);
        var result = await TestApp.SendAsync(query);

        // Assert
        result.ShouldNotBeNull();
        result.ContentType.ShouldBe("application/xml; charset=utf-8");
        result.FileName.ShouldBe("FACTURA_001_001_000000201.xml");
        result.Content.ShouldNotBeNull();
        result.Content.Length.ShouldBeGreaterThan(100);

        string xmlStr = Encoding.UTF8.GetString(result.Content);
        xmlStr.ShouldContain("<autorizacion>");
        xmlStr.ShouldContain("<estado>AUTORIZADO</estado>");
        xmlStr.ShouldContain($"<numeroAutorizacion>{claveAcceso}</numeroAutorizacion>");
    }

    [Test]
    public async Task GetFacturaXml_ModoRaw_DebeRetornarSoloFacturaXml()
    {
        // Arrange
        await TestApp.RunAsDefaultUserAsync();
        var tenantId = TestApp.GetTenantId()!.Value;

        var emisor = Emisor.Crear(tenantId, "0957790108001", "EMPRESA PRUEBA S.A.", "Av. Principal 100");
        await TestApp.AddAsync(emisor);

        var comprador = Comprador.Crear("07", "9999999999999", "CONSUMIDOR FINAL");
        var detalle = DetalleFactura.Crear("SERV-02", "Almuerzo", 1, 10m, 0, [Impuesto.Crear("2", "0", 0m, 10m)]);

        var factura = Factura.Crear(
            tenantId, 1, emisor.RazonSocial, emisor.Ruc,
            "001", "001", "000000202", emisor.DireccionMatriz,
            DateTime.UtcNow, comprador, [detalle], emisor.Id);

        string claveAcceso = "1609202601095779010800110010010000002021234567813";
        factura.AsignarClaveAcceso(claveAcceso);
        factura.MarcarComoAutorizada(claveAcceso, DateTime.UtcNow);

        await TestApp.AddAsync(factura);

        // Act (Raw = true)
        var query = new GetFacturaXmlQuery(factura.Id, Raw: true);
        var result = await TestApp.SendAsync(query);

        // Assert
        result.ShouldNotBeNull();
        string xmlStr = Encoding.UTF8.GetString(result.Content);
        xmlStr.ShouldNotContain("<autorizacion>");
        xmlStr.ShouldContain("<factura id=\"comprobante\" version=\"1.1.0\">");
    }

    [Test]
    public async Task GetFacturaXml_FacturaDeOtroTenant_DebeLanzarUnauthorizedAccessException()
    {
        // Arrange
        await TestApp.RunAsDefaultUserAsync();

        var otroTenantId = Guid.NewGuid();
        var otroEmisor = Emisor.Crear(otroTenantId, "0957790108002", "OTRA EMPRESA", "Guayaquil");
        await TestApp.AddAsync(otroEmisor);

        var comprador = Comprador.Crear("07", "9999999999999", "CONSUMIDOR FINAL");
        var detalle = DetalleFactura.Crear("SERV-01", "Item", 1, 10m, 0, [Impuesto.Crear("2", "0", 0m, 10m)]);

        var factura = Factura.Crear(
            otroTenantId, 1, otroEmisor.RazonSocial, otroEmisor.Ruc,
            "001", "001", "000000888", otroEmisor.DireccionMatriz,
            DateTime.UtcNow, comprador, [detalle], otroEmisor.Id);
        factura.AsignarClaveAcceso("1609202601095779010800210010010000008881234567813");

        await TestApp.AddAsync(factura);

        // Act & Assert
        var query = new GetFacturaXmlQuery(factura.Id);
        await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
        {
            await TestApp.SendAsync(query);
        });
    }

    [Test]
    public async Task GetFacturaXml_FacturaInexistente_DebeLanzarNotFoundException()
    {
        await TestApp.RunAsDefaultUserAsync();

        var query = new GetFacturaXmlQuery(999999);

        await Should.ThrowAsync<NotFoundException>(async () =>
        {
            await TestApp.SendAsync(query);
        });
    }
}

