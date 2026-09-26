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

    [Test]
    public void GenerarXml_ConRegimenRimpeEmprendedor_DeberiaIncluirEtiquetaContribuyenteRimpe()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var comprador = Comprador.Crear("07", "9999999999999", "CONSUMIDOR FINAL");
        var detalles = new List<DetalleFactura>
        {
            DetalleFactura.Crear("SRV-1", "Servicio General", 1, 10m, 0,
                [Impuesto.Crear("2", "4", 15m, 10m)])
        };

        var factura = Factura.Crear(
            tenantId, 1, "EMPRENDEDOR SA", "0957790108001",
            "001", "001", "000000101", "Matriz",
            DateTime.UtcNow, comprador, detalles,
            contribuyenteRimpe: "CONTRIBUYENTE RÉGIMEN RIMPE");
        factura.AsignarClaveAcceso("1609202601095779010800110010010000001011234567813");

        // Act
        byte[] xmlBytes = _xmlGenerator.GenerarXmlBytes(factura);
        string xmlStr = Encoding.UTF8.GetString(xmlBytes);

        // Assert: Valida etiqueta oficial en infoTributaria y campoAdicional
        xmlStr.ShouldContain("<contribuyenteRimpe>CONTRIBUYENTE RÉGIMEN RIMPE</contribuyenteRimpe>");
        xmlStr.ShouldContain("<campoAdicional nombre=\"Regimen\">CONTRIBUYENTE RÉGIMEN RIMPE</campoAdicional>");
    }

    [Test]
    public void GenerarXml_ConRegimenRimpeNegocioPopular_DeberiaIncluirEtiquetaNegocioPopular()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var comprador = Comprador.Crear("07", "9999999999999", "CONSUMIDOR FINAL");
        var detalles = new List<DetalleFactura>
        {
            DetalleFactura.Crear("NP-01", "Venta Menor", 1, 5m, 0,
                [Impuesto.Crear("2", "0", 0m, 5m)])
        };

        var factura = Factura.Crear(
            tenantId, 1, "BAZAR DON PEPE", "0957790108001",
            "001", "001", "000000202", "Matriz",
            DateTime.UtcNow, comprador, detalles,
            contribuyenteRimpe: "CONTRIBUYENTE NEGOCIO POPULAR - RÉGIMEN RIMPE");
        factura.AsignarClaveAcceso("1609202601095779010800110010010000002021234567813");

        // Act
        byte[] xmlBytes = _xmlGenerator.GenerarXmlBytes(factura);
        string xmlStr = Encoding.UTF8.GetString(xmlBytes);

        // Assert: Valida leyenda exacta oficial requerida por el SRI
        xmlStr.ShouldContain("<contribuyenteRimpe>CONTRIBUYENTE NEGOCIO POPULAR - RÉGIMEN RIMPE</contribuyenteRimpe>");
        xmlStr.ShouldContain("<campoAdicional nombre=\"Regimen\">CONTRIBUYENTE NEGOCIO POPULAR - RÉGIMEN RIMPE</campoAdicional>");
    }

    [Test]
    public void GenerarXml_RegimenGeneral_NoDeberiaIncluirEtiquetaContribuyenteRimpe()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var comprador = Comprador.Crear("07", "9999999999999", "CONSUMIDOR FINAL");
        var detalles = new List<DetalleFactura>
        {
            DetalleFactura.Crear("GEN-1", "Venta General", 1, 20m, 0,
                [Impuesto.Crear("2", "4", 15m, 20m)])
        };

        var factura = Factura.Crear(
            tenantId, 1, "EMPRESA GENERAL SA", "0957790108001",
            "001", "001", "000000303", "Matriz",
            DateTime.UtcNow, comprador, detalles,
            contribuyenteRimpe: null);
        factura.AsignarClaveAcceso("1609202601095779010800110010010000003031234567813");

        // Act
        byte[] xmlBytes = _xmlGenerator.GenerarXmlBytes(factura);
        string xmlStr = Encoding.UTF8.GetString(xmlBytes);

        // Assert
        xmlStr.ShouldNotContain("<contribuyenteRimpe>");
    }

    [Test]
    public void GenerarXml_ConFormaPagoPorDefecto_DeberiaGenerarPagos01SinPlazo()
    {
        // Arrange
        var comprador = Comprador.Crear("07", "9999999999999", "CONSUMIDOR FINAL");
        var detalles = new List<DetalleFactura>
        {
            DetalleFactura.Crear("P1", "Item 1", 1, 10m, 0, [Impuesto.Crear("2", "4", 15m, 10m)])
        };

        var factura = Factura.Crear(
            Guid.NewGuid(), 1, "EMPRESA", "0957790108001",
            "001", "001", "000000001", "Matriz",
            DateTime.UtcNow, comprador, detalles);
        factura.AsignarClaveAcceso("1609202601095779010800110010010000000011234567813");

        // Act
        byte[] xmlBytes = _xmlGenerator.GenerarXmlBytes(factura);
        string xmlStr = Encoding.UTF8.GetString(xmlBytes);

        // Assert
        xmlStr.ShouldContain("<pagos>");
        xmlStr.ShouldContain("<formaPago>01</formaPago>");
        xmlStr.ShouldContain("<total>11.50</total>");
        xmlStr.ShouldNotContain("<plazo>");
        xmlStr.ShouldNotContain("<unidadTiempo>");
    }

    [Test]
    public void GenerarXml_ConFormaPagoPersonalizadaYPlazo_DeberiaGenerarPagosConPlazoYUnidadTiempo()
    {
        // Arrange
        var comprador = Comprador.Crear("04", "1792060346001", "CLIENTE SA", "Quito");
        var detalles = new List<DetalleFactura>
        {
            DetalleFactura.Crear("P1", "Item Credito", 1, 100m, 0, [Impuesto.Crear("2", "4", 15m, 100m)])
        };

        var factura = Factura.Crear(
            Guid.NewGuid(), 1, "EMPRESA", "0957790108001",
            "001", "001", "000000001", "Matriz",
            DateTime.UtcNow, comprador, detalles,
            formaPago: "20",
            plazo: 30,
            unidadTiempo: "dias");
        factura.AsignarClaveAcceso("1609202601095779010800110010010000000011234567813");

        // Act
        byte[] xmlBytes = _xmlGenerator.GenerarXmlBytes(factura);
        string xmlStr = Encoding.UTF8.GetString(xmlBytes);

        // Assert
        xmlStr.ShouldContain("<pagos>");
        xmlStr.ShouldContain("<formaPago>20</formaPago>");
        xmlStr.ShouldContain("<total>115.00</total>");
        xmlStr.ShouldContain("<plazo>30.00</plazo>");
        xmlStr.ShouldContain("<unidadTiempo>dias</unidadTiempo>");
    }
}

