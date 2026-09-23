using System;
using System.Collections.Generic;
using System.Text;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Infrastructure.Services;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.UnitTests.NotasCredito.Queries;

[TestFixture]
public class RideNotaCreditoPdfGeneratorTests
{
    private RidePdfGenerator _pdfGenerator = null!;

    [SetUp]
    public void SetUp()
    {
        _pdfGenerator = new RidePdfGenerator();
    }

    [Test]
    public void GenerarNotaCreditoRide_DeberiaGenerarPdfValido_ConCabeceraPdfYLongitudMayorACero()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var emisor = Emisor.Crear(
            tenantId: tenantId,
            ruc: "0957790108001",
            razonSocial: "EMPRESA DE PRUEBA S.A.",
            direccionMatriz: "Av. Amazonas y Naciones Unidas",
            codigoEstablecimiento: "001",
            puntoEmision: "002",
            ambiente: 1,
            obligadoContabilidad: true,
            nombreComercial: "COMERCIAL PRUEBA",
            direccionEstablecimiento: "Sucursal Norte",
            regimenRimpe: "CONTRIBUYENTE RÉGIMEN RIMPE",
            contribuyenteEspecial: "5368"
        );

        var cliente = Comprador.Crear(
            tipoIdentificacion: "04",
            identificacion: "1792060346001",
            razonSocial: "CLIENTE DE PRUEBAS CIA. LTDA.",
            direccion: "Calle Los Pinos 123",
            correoElectronico: "facturacion@clienteprueba.com"
        );

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
            ambiente: emisor.Ambiente,
            razonSocial: emisor.RazonSocial,
            rucEmisor: emisor.Ruc,
            establecimiento: emisor.CodigoEstablecimiento,
            puntoEmision: emisor.PuntoEmision,
            secuencial: "000000123",
            direccionMatriz: emisor.DireccionMatriz,
            fechaEmision: new DateTime(2026, 9, 21),
            cliente: cliente,
            numDocModificado: "001-001-000000100",
            fechaEmisionDocSustento: new DateTime(2026, 9, 10),
            motivo: "Devolución total",
            detalles: [detalle]
        );

        string claveAcceso = "2109202604095779010800110010020000001231234567814";
        notaCredito.AsignarClaveAcceso(claveAcceso);
        notaCredito.MarcarComoAutorizada(claveAcceso, DateTime.UtcNow);

        // Act
        var pdfBytes = _pdfGenerator.GenerarNotaCreditoRide(notaCredito, emisor);

        // Assert
        pdfBytes.ShouldNotBeNull();
        pdfBytes.Length.ShouldBeGreaterThan(1000);

        var pdfHeader = Encoding.ASCII.GetString(pdfBytes, 0, 5);
        pdfHeader.ShouldBe("%PDF-");
    }

    [Test]
    public void GenerarNotaCreditoRide_ConEmisorNulo_DeberiaUsarDatosDeNotaCreditoYGenerarPdfValido()
    {
        // Arrange
        var cliente = Comprador.Crear(
            tipoIdentificacion: "05",
            identificacion: "1722889900",
            razonSocial: "JUAN PEREZ",
            direccion: "Quito Sur",
            correoElectronico: "juan@correo.com"
        );

        var detalle = DetalleNotaCredito.Crear(
            codigoPrincipal: "SERV-01",
            descripcion: "Descuento en servicios",
            cantidad: 1,
            precioUnitario: 35.00m,
            descuento: 0,
            impuestos: [Impuesto.Crear("2", "4", 15.00m, 35.00m)]
        );

        var notaCredito = NotaCredito.Crear(
            tenantId: Guid.NewGuid(),
            ambiente: 1,
            razonSocial: "CORPORACION MATRIZ CIA LTDA",
            rucEmisor: "1790011223001",
            establecimiento: "002",
            puntoEmision: "001",
            secuencial: "000000099",
            direccionMatriz: "Av. Shyris y Naciones Unidas",
            fechaEmision: DateTime.UtcNow,
            cliente: cliente,
            numDocModificado: "002-001-000000050",
            fechaEmisionDocSustento: DateTime.UtcNow.AddDays(-15),
            motivo: "Ajuste tarifario convenido",
            detalles: [detalle]
        );

        notaCredito.AsignarClaveAcceso("2109202604179001122300110020010000000991234567814");

        // Act
        var pdfBytes = _pdfGenerator.GenerarNotaCreditoRide(notaCredito);

        // Assert
        pdfBytes.ShouldNotBeNull();
        pdfBytes.Length.ShouldBeGreaterThan(1000);

        var pdfHeader = Encoding.ASCII.GetString(pdfBytes, 0, 5);
        pdfHeader.ShouldBe("%PDF-");
    }
}
