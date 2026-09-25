using System;
using System.Collections.Generic;
using BillingSaaS.Domain.Entities;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Domain.UnitTests.Entities;

[TestFixture]
public class FacturaTests
{
    private Guid _tenantId;
    private Comprador _consumidorFinal = null!;
    private Comprador _clienteIdentificado = null!;

    [SetUp]
    public void SetUp()
    {
        _tenantId = Guid.NewGuid();
        _consumidorFinal = Comprador.Crear("07", "9999999999999", "CONSUMIDOR FINAL", "Quito");
        _clienteIdentificado = Comprador.Crear("04", "1790012345001", "EMPRESA PRUEBA S.A.", "Guayaquil", "test@empresa.com");
    }

    [Test]
    public void Crear_ConsumidorFinalConMontoExactoCincuenta_DebePermitirse()
    {
        var detalle = DetalleFactura.Crear(
            "PROD01",
            "Item de prueba",
            cantidad: 1,
            precioUnitario: 50.00m,
            descuento: 0m,
            impuestos: [Impuesto.Crear("2", "0", 0m, 50.00m)]
        );

        var factura = Factura.Crear(
            _tenantId,
            ambiente: 1,
            razonSocial: "EMISOR S.A.",
            rucEmisor: "0957790108001",
            establecimiento: "001",
            puntoEmision: "001",
            secuencial: "000000001",
            direccionMatriz: "Quito",
            fechaEmision: DateTime.UtcNow.Date,
            cliente: _consumidorFinal,
            detalles: [detalle],
            emisorId: 1
        );

        factura.ImporteTotal.ShouldBe(50.00m);
    }

    [Test]
    public void Crear_ConsumidorFinalConMontoMayorACincuenta_DebeLanzarExcepcion()
    {
        var detalle = DetalleFactura.Crear(
            "PROD01",
            "Item de prueba",
            cantidad: 1,
            precioUnitario: 50.01m,
            descuento: 0m,
            impuestos: [Impuesto.Crear("2", "0", 0m, 50.01m)]
        );

        var ex = Should.Throw<InvalidOperationException>(() =>
            Factura.Crear(
                _tenantId,
                ambiente: 1,
                razonSocial: "EMISOR S.A.",
                rucEmisor: "0957790108001",
                establecimiento: "001",
                puntoEmision: "001",
                secuencial: "000000001",
                direccionMatriz: "Quito",
                fechaEmision: DateTime.UtcNow.Date,
                cliente: _consumidorFinal,
                detalles: [detalle],
                emisorId: 1
            ));

        ex.Message.ShouldContain("El importe supera los $50.00 USD");
    }

    [Test]
    public void Crear_ClienteIdentificadoConMontoMayorACincuenta_DebePermitirse()
    {
        var detalle = DetalleFactura.Crear(
            "PROD01",
            "Item de prueba",
            cantidad: 1,
            precioUnitario: 500.00m,
            descuento: 0m,
            impuestos: [Impuesto.Crear("2", "4", 15m, 500.00m)]
        );

        var factura = Factura.Crear(
            _tenantId,
            ambiente: 1,
            razonSocial: "EMISOR S.A.",
            rucEmisor: "0957790108001",
            establecimiento: "001",
            puntoEmision: "001",
            secuencial: "000000001",
            direccionMatriz: "Quito",
            fechaEmision: DateTime.UtcNow.Date,
            cliente: _clienteIdentificado,
            detalles: [detalle],
            emisorId: 1
        );

        factura.ImporteTotal.ShouldBe(575.00m);
    }
}
