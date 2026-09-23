using System;
using System.Collections.Generic;
using System.Text;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Infrastructure.Servicios;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.UnitTests.NotasCredito.Queries;

[TestFixture]
public class NotaCreditoXmlGeneratorTests
{
    private NotaCreditoXmlGenerator _xmlGenerator = null!;

    [SetUp]
    public void SetUp()
    {
        _xmlGenerator = new NotaCreditoXmlGenerator("0957790108001");
    }

    [Test]
    public void GenerarXmlBytes_DeberiaGenerarXmlValido_SinBomUtf8()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var comprador = Comprador.Crear("04", "1792060346001", "CLIENTE PRUEBA SA", "Quito", "info@prueba.com");
        var detalle = DetalleNotaCredito.Crear(
            codigoPrincipal: "PROD-01",
            descripcion: "Devolución de mercadería",
            cantidad: 2,
            precioUnitario: 50.00m,
            descuento: 0,
            impuestos: [Impuesto.Crear("2", "4", 15.00m, 100.00m)]
        );

        var notaCredito = NotaCredito.Crear(
            tenantId: tenantId,
            ambiente: 1,
            razonSocial: "MI EMPRESA SA",
            rucEmisor: "0957790108001",
            establecimiento: "001",
            puntoEmision: "001",
            secuencial: "000000001",
            direccionMatriz: "Av. Amazonas 100",
            fechaEmision: new DateTime(2026, 9, 21),
            cliente: comprador,
            numDocModificado: "001-001-000000100",
            fechaEmisionDocSustento: new DateTime(2026, 9, 10),
            motivo: "Devolución por defecto de fábrica",
            detalles: [detalle]
        );

        notaCredito.AsignarClaveAcceso("2109202604095779010800110010010000000011234567814");

        // Act
        byte[] xmlBytes = _xmlGenerator.GenerarXmlBytes(notaCredito);

        // Assert
        xmlBytes.ShouldNotBeNull();
        xmlBytes.Length.ShouldBeGreaterThan(100);

        bool hasBom = xmlBytes.Length >= 3 && xmlBytes[0] == 0xEF && xmlBytes[1] == 0xBB && xmlBytes[2] == 0xBF;
        hasBom.ShouldBeFalse("El SRI rechaza XMLs con BOM UTF-8");

        string xmlStr = Encoding.UTF8.GetString(xmlBytes);
        xmlStr.ShouldStartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        xmlStr.ShouldContain("<notaCredito id=\"comprobante\" version=\"1.1.0\">");
        xmlStr.ShouldContain("<codDoc>04</codDoc>");
        xmlStr.ShouldContain("<claveAcceso>2109202604095779010800110010010000000011234567814</claveAcceso>");
        xmlStr.ShouldContain("<codDocModificado>01</codDocModificado>");
        xmlStr.ShouldContain("<numDocModificado>001-001-000000100</numDocModificado>");
        xmlStr.ShouldContain("<fechaEmisionDocSustento>10/09/2026</fechaEmisionDocSustento>");
        xmlStr.ShouldContain("<totalSinImpuestos>100.00</totalSinImpuestos>");
        xmlStr.ShouldContain("<valorModificacion>115.00</valorModificacion>");
        xmlStr.ShouldContain("<motivo>Devolución por defecto de fábrica</motivo>");
        xmlStr.ShouldContain("<detalles>");
        xmlStr.ShouldContain("<codigoInterno>PROD-01</codigoInterno>");
        xmlStr.ShouldContain("<descripcion>Devolución de mercadería</descripcion>");
        xmlStr.ShouldContain("<totalConImpuestos>");
    }

    [Test]
    public void GenerarXmlAutorizadoBytes_NotaCreditoAutorizada_DeberiaGenerarEstructuraAutorizacionSri()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var comprador = Comprador.Crear("04", "1792060346001", "CLIENTE PRUEBA SA", "Quito", "info@prueba.com");
        var detalle = DetalleNotaCredito.Crear(
            codigoPrincipal: "PROD-02",
            descripcion: "Descuento comercial",
            cantidad: 1,
            precioUnitario: 20.00m,
            descuento: 0,
            impuestos: [Impuesto.Crear("2", "4", 15.00m, 20.00m)]
        );

        var notaCredito = NotaCredito.Crear(
            tenantId: tenantId,
            ambiente: 1,
            razonSocial: "MI EMPRESA SA",
            rucEmisor: "0957790108001",
            establecimiento: "001",
            puntoEmision: "001",
            secuencial: "000000001",
            direccionMatriz: "Av. Amazonas 100",
            fechaEmision: DateTime.UtcNow,
            cliente: comprador,
            numDocModificado: "001-001-000000100",
            fechaEmisionDocSustento: DateTime.UtcNow.AddDays(-5),
            motivo: "Descuento acordado",
            detalles: [detalle]
        );

        string claveAcceso = "2109202604095779010800110010010000000551234567814";
        notaCredito.AsignarClaveAcceso(claveAcceso);
        notaCredito.MarcarComoAutorizada(claveAcceso, new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc));

        // Act
        byte[] xmlBytes = _xmlGenerator.GenerarXmlAutorizadoBytes(notaCredito);
        string xmlStr = Encoding.UTF8.GetString(xmlBytes);

        // Assert
        xmlStr.ShouldContain("<autorizacion>");
        xmlStr.ShouldContain("<estado>AUTORIZADO</estado>");
        xmlStr.ShouldContain($"<numeroAutorizacion>{claveAcceso}</numeroAutorizacion>");
        xmlStr.ShouldContain("<comprobante><![CDATA[");
        xmlStr.ShouldContain("</autorizacion>");
    }
}
