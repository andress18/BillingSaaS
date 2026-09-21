using System;
using System.Collections.Generic;
using System.Text;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Infrastructure.Servicios;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.UnitTests.NotasDebito.Queries;

[TestFixture]
public class NotaDebitoXmlGeneratorTests
{
    private NotaDebitoXmlGenerator _xmlGenerator = null!;

    [SetUp]
    public void SetUp()
    {
        _xmlGenerator = new NotaDebitoXmlGenerator("0957790108001");
    }

    [Test]
    public void GenerarXmlBytes_DeberiaGenerarXmlValido_SinBomUtf8()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var comprador = Comprador.Crear("04", "1792060346001", "CLIENTE PRUEBA SA", "Quito", "info@prueba.com");
        var motivos = new List<MotivoNotaDebito>
        {
            MotivoNotaDebito.Crear("Intereses por mora factura 001-001-000000100", 80.00m)
        };
        var impuestos = new List<ImpuestoNotaDebito>
        {
            ImpuestoNotaDebito.Crear("2", "4", 15.00m, 80.00m) // IVA 15% = 12.00
        };

        var notaDebito = NotaDebito.Crear(
            tenantId, 1, "MI EMPRESA SA", "0957790108001",
            "001", "001", "000000001", "Av. Amazonas 100",
            new DateTime(2026, 9, 21), comprador,
            "001-001-000000100", new DateTime(2026, 9, 10),
            motivos, impuestos);

        notaDebito.AsignarClaveAcceso("2109202605095779010800110010010000000011234567814");

        // Act
        byte[] xmlBytes = _xmlGenerator.GenerarXmlBytes(notaDebito);

        // Assert
        xmlBytes.ShouldNotBeNull();
        xmlBytes.Length.ShouldBeGreaterThan(100);

        bool hasBom = xmlBytes.Length >= 3 && xmlBytes[0] == 0xEF && xmlBytes[1] == 0xBB && xmlBytes[2] == 0xBF;
        hasBom.ShouldBeFalse("El SRI rechaza XMLs con BOM UTF-8");

        string xmlStr = Encoding.UTF8.GetString(xmlBytes);
        xmlStr.ShouldStartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        xmlStr.ShouldContain("<notaDebito id=\"comprobante\" version=\"1.0.0\">");
        xmlStr.ShouldContain("<codDoc>05</codDoc>");
        xmlStr.ShouldContain("<claveAcceso>2109202605095779010800110010010000000011234567814</claveAcceso>");
        xmlStr.ShouldContain("<codDocModificado>01</codDocModificado>");
        xmlStr.ShouldContain("<numDocModificado>001-001-000000100</numDocModificado>");
        xmlStr.ShouldContain("<fechaEmisionDocSustento>10/09/2026</fechaEmisionDocSustento>");
        xmlStr.ShouldContain("<totalSinImpuestos>80.00</totalSinImpuestos>");
        xmlStr.ShouldContain("<valorTotal>92.00</valorTotal>");
        xmlStr.ShouldContain("<motivos>");
        xmlStr.ShouldContain("<razon>Intereses por mora factura 001-001-000000100</razon>");
        xmlStr.ShouldContain("<valor>80.00</valor>");
        xmlStr.ShouldContain("<impuestos>");
        xmlStr.ShouldContain("<pagos>");
    }

    [Test]
    public void GenerarXmlAutorizadoBytes_NotaDebitoAutorizada_DeberiaGenerarEstructuraAutorizacionSri()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var comprador = Comprador.Crear("04", "1792060346001", "CLIENTE PRUEBA SA", "Quito", "info@prueba.com");
        var notaDebito = NotaDebito.Crear(
            tenantId, 1, "MI EMPRESA SA", "0957790108001",
            "001", "001", "000000001", "Av. Amazonas 100",
            DateTime.UtcNow, comprador,
            "001-001-000000100", DateTime.UtcNow.AddDays(-5),
            [MotivoNotaDebito.Crear("Ajuste", 10m)],
            [ImpuestoNotaDebito.Crear("2", "4", 15m, 10m)]);

        string claveAcceso = "2109202605095779010800110010010000000551234567814";
        notaDebito.AsignarClaveAcceso(claveAcceso);
        notaDebito.MarcarComoAutorizada(claveAcceso, new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc));

        // Act
        byte[] xmlBytes = _xmlGenerator.GenerarXmlAutorizadoBytes(notaDebito);
        string xmlStr = Encoding.UTF8.GetString(xmlBytes);

        // Assert
        xmlStr.ShouldContain("<autorizacion>");
        xmlStr.ShouldContain("<estado>AUTORIZADO</estado>");
        xmlStr.ShouldContain($"<numeroAutorizacion>{claveAcceso}</numeroAutorizacion>");
        xmlStr.ShouldContain("<comprobante><![CDATA[");
        xmlStr.ShouldContain("</autorizacion>");
    }
}

