using System;
using System.Collections.Generic;
using BillingSaaS.Domain.Entities;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Domain.UnitTests.Entities;

[TestFixture]
public class NotaDebitoTests
{
    private Comprador _clienteDefault = null!;

    [SetUp]
    public void SetUp()
    {
        _clienteDefault = Comprador.Crear("04", "1790012345001", "CLIENTE EMPRESA S.A.", "Av. Amazonas 123", "cliente@empresa.com");
    }

    [Test]
    public void Crear_ConDatosValidos_DebeCalcularTotalesYValoresCorrectos()
    {
        var tenantId = Guid.NewGuid();
        var motivos = new List<MotivoNotaDebito>
        {
            MotivoNotaDebito.Crear("Intereses por mora de factura 001-001-000000100", 100.00m),
            MotivoNotaDebito.Crear("Recargo administrativo por cobranza", 20.00m)
        };

        var impuestos = new List<ImpuestoNotaDebito>
        {
            ImpuestoNotaDebito.Crear("2", "4", 15.00m, 120.00m) // IVA 15% sobre 120 = 18.00
        };

        var notaDebito = NotaDebito.Crear(
            tenantId: tenantId,
            ambiente: 1,
            razonSocial: "EMISOR PRUEBA CIA LTDA",
            rucEmisor: "0957790108001",
            establecimiento: "001",
            puntoEmision: "001",
            secuencial: "000000001",
            direccionMatriz: "Quito, Ecuador",
            fechaEmision: DateTime.UtcNow.Date,
            cliente: _clienteDefault,
            numDocModificado: "001-001-000000100",
            fechaEmisionDocSustento: DateTime.UtcNow.Date.AddDays(-10),
            motivos: motivos,
            impuestos: impuestos
        );

        notaDebito.TenantId.ShouldBe(tenantId);
        notaDebito.CodDoc.ShouldBe("05");
        notaDebito.CodDocModificado.ShouldBe("01");
        notaDebito.NumDocModificado.ShouldBe("001-001-000000100");
        notaDebito.TotalSinImpuestos.ShouldBe(120.00m);
        notaDebito.ValorTotal.ShouldBe(138.00m);
        notaDebito.Estado.ShouldBe("CREADA");
        notaDebito.Motivos.Count.ShouldBe(2);
        notaDebito.Impuestos.Count.ShouldBe(1);
        notaDebito.Pagos.Count.ShouldBe(1);
        notaDebito.Pagos.First().Total.ShouldBe(138.00m);
    }

    [Test]
    public void AsignarClaveAcceso_ConClaveValida_DebeAsignarClave()
    {
        var notaDebito = CrearNotaDebitoBase();
        var clave49 = new string('5', 49);

        notaDebito.AsignarClaveAcceso(clave49);

        notaDebito.ClaveAcceso.ShouldBe(clave49);
    }

    [Test]
    public void AsignarClaveAcceso_ConLongitudInvalida_DebeLanzarExcepcion()
    {
        var notaDebito = CrearNotaDebitoBase();

        Should.Throw<ArgumentException>(() => notaDebito.AsignarClaveAcceso("12345"));
    }

    [Test]
    public void MarcarComoAutorizada_DebeActualizarEstadoYValores()
    {
        var notaDebito = CrearNotaDebitoBase();
        var numAut = "1234567890123456789012345678901234567890123456789";
        var fechaAut = DateTime.UtcNow;

        notaDebito.MarcarComoAutorizada(numAut, fechaAut, "<xml>firmado</xml>");

        notaDebito.Estado.ShouldBe("AUTORIZADO");
        notaDebito.NumeroAutorizacion.ShouldBe(numAut);
        notaDebito.FechaAutorizacion.ShouldBe(fechaAut);
        notaDebito.XmlFirmado.ShouldBe("<xml>firmado</xml>");
    }

    [Test]
    public void MarcarComoDevuelta_DebeActualizarEstadoYMensaje()
    {
        var notaDebito = CrearNotaDebitoBase();

        notaDebito.MarcarComoDevuelta("ERROR TRIBUTARIO EN SRI");

        notaDebito.Estado.ShouldBe("DEVUELTA");
        notaDebito.MensajeErrorSri.ShouldBe("ERROR TRIBUTARIO EN SRI");
    }

    [Test]
    public void MarcarComoNoAutorizada_DebeActualizarEstadoYMensaje()
    {
        var notaDebito = CrearNotaDebitoBase();

        notaDebito.MarcarComoNoAutorizada("RECHAZADO POR FIRMA INVALIDA");

        notaDebito.Estado.ShouldBe("NO AUTORIZADO");
        notaDebito.MensajeErrorSri.ShouldBe("RECHAZADO POR FIRMA INVALIDA");
    }

    private NotaDebito CrearNotaDebitoBase()
    {
        return NotaDebito.Crear(
            tenantId: Guid.NewGuid(),
            ambiente: 1,
            razonSocial: "EMISOR PRUEBA CIA LTDA",
            rucEmisor: "0957790108001",
            establecimiento: "001",
            puntoEmision: "001",
            secuencial: "000000001",
            direccionMatriz: "Quito, Ecuador",
            fechaEmision: DateTime.UtcNow.Date,
            cliente: _clienteDefault,
            numDocModificado: "001-001-000000100",
            fechaEmisionDocSustento: DateTime.UtcNow.Date.AddDays(-10),
            motivos: [MotivoNotaDebito.Crear("Mora", 50m)],
            impuestos: [ImpuestoNotaDebito.Crear("2", "4", 15m, 50m)]
        );
    }
}

