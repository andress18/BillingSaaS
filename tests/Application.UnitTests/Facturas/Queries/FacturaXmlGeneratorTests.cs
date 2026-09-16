using System;
using System.Collections.Generic;
using System.Text;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Infrastructure.Servicios;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.UnitTests.Facturas.Queries;

[TestFixture]
public class FacturaXmlGeneratorTests
{
    private FacturaXmlGenerator _xmlGenerator = null!;

    [SetUp]
    public void SetUp()
    {
        _xmlGenerator = new FacturaXmlGenerator("0957790108001");
    }

    [Test]
    public void GenerarXmlBytes_DeberiaGenerarXmlValido_SinBomUtf8()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var comprador = Comprador.Crear("04", "1792060346001", "CLIENTE PRUEBA SA", "Quito", "info@prueba.com");
        var detalles = new List<DetalleFactura>
        {
            DetalleFactura.Crear("P01", "Licencia SaaS Mensual", 1, 50m, 0,
                [Impuesto.Crear("2", "4", 15m, 50m)])
        };

        var factura = Factura.Crear(
            tenantId, 1, "MI EMPRESA SA", "0957790108001",
            "001", "001", "000000001", "Av. Amazonas 100",
            DateTime.UtcNow, comprador, detalles);
        factura.AsignarClaveAcceso("1609202601095779010800110010010000000011234567813");

        // Act
        byte[] xmlBytes = _xmlGenerator.GenerarXmlBytes(factura);

        // Assert
        xmlBytes.ShouldNotBeNull();
        xmlBytes.Length.ShouldBeGreaterThan(100);

        // Validar que NO contiene BOM UTF-8 (EF BB BF)
        bool hasBom = xmlBytes.Length >= 3 && xmlBytes[0] == 0xEF && xmlBytes[1] == 0xBB && xmlBytes[2] == 0xBF;
        hasBom.ShouldBeFalse("El SRI rechaza XMLs con BOM UTF-8");

        string xmlStr = Encoding.UTF8.GetString(xmlBytes);
        xmlStr.ShouldStartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        xmlStr.ShouldContain("<factura id=\"comprobante\" version=\"1.1.0\">");
        xmlStr.ShouldContain("<claveAcceso>1609202601095779010800110010010000000011234567813</claveAcceso>");
        xmlStr.ShouldContain("<totalSinImpuestos>50.00</totalSinImpuestos>");
    }

    [Test]
    public void GenerarXmlAutorizadoBytes_FacturaAutorizada_DeberiaGenerarEstructuraAutorizacionSri()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var comprador = Comprador.Crear("05", "0921234567", "JUAN PEREZ");
        var detalles = new List<DetalleFactura>
        {
            DetalleFactura.Crear("SRV-1", "Consultoria", 1, 20m, 0,
                [Impuesto.Crear("2", "0", 0m, 20m)])
        };

        var factura = Factura.Crear(
            tenantId, 1, "MI EMPRESA SA", "0957790108001",
            "001", "001", "000000055", "Matriz",
            DateTime.UtcNow, comprador, detalles);

        string claveAcceso = "1609202601095779010800110010010000000551234567813";
        factura.AsignarClaveAcceso(claveAcceso);
        factura.MarcarComoAutorizada(claveAcceso, new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc));

        // Act
        byte[] xmlBytes = _xmlGenerator.GenerarXmlAutorizadoBytes(factura);

        // Assert
        string xmlStr = Encoding.UTF8.GetString(xmlBytes);
        xmlStr.ShouldContain("<autorizacion>");
        xmlStr.ShouldContain("<estado>AUTORIZADO</estado>");
        xmlStr.ShouldContain($"<numeroAutorizacion>{claveAcceso}</numeroAutorizacion>");
        xmlStr.ShouldContain("<comprobante><![CDATA[");
        xmlStr.ShouldContain("<factura id=\"comprobante\" version=\"1.1.0\">");
        xmlStr.ShouldContain("]]></comprobante>");
    }

    [Test]
    public void GenerarXmlAutorizadoBytes_ConXmlFirmadoExistente_DeberiaIncrustarXmlFirmadoEnCData()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var comprador = Comprador.Crear("07", "9999999999999", "CONSUMIDOR FINAL");
        var detalles = new List<DetalleFactura>
        {
            DetalleFactura.Crear("PROD-99", "Item Generico", 1, 10m, 0,
                [Impuesto.Crear("2", "0", 0m, 10m)])
        };

        var factura = Factura.Crear(
            tenantId, 1, "EMPRESA SA", "0957790108001",
            "001", "001", "000000088", "Matriz",
            DateTime.UtcNow, comprador, detalles);

        string claveAcceso = "1609202601095779010800110010010000000881234567813";
        factura.AsignarClaveAcceso(claveAcceso);
        factura.MarcarComoAutorizada(claveAcceso, DateTime.UtcNow);

        const string signedXmlMock = "<factura id=\"comprobante\"><ds:Signature xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\">FIRMA_MOCK</ds:Signature></factura>";

        // Act
        byte[] xmlBytes = _xmlGenerator.GenerarXmlAutorizadoBytes(factura, signedXmlMock);

        // Assert
        string xmlStr = Encoding.UTF8.GetString(xmlBytes);
        xmlStr.ShouldContain("<comprobante><![CDATA[<factura id=\"comprobante\"><ds:Signature xmlns:ds=\"http://www.w3.org/2000/09/xmldsig#\">FIRMA_MOCK</ds:Signature></factura>]]></comprobante>");
    }
}

