using System;
using System.Collections.Generic;
using BillingSaaS.Domain.Entities;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Domain.UnitTests.Entities;

[TestFixture]
public class NotaCreditoTests
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
        var detalles = new List<DetalleNotaCredito>
        {
            DetalleNotaCredito.Crear(
                "PROD-001",
                "Teclado mecánico USB",
                2,
                50.00m,
                0m,
                new List<Impuesto>
                {
                    Impuesto.Crear("2", "4", 15.00m, 100.00m) // IVA 15% sobre 100 = 15.00
                }),
            DetalleNotaCredito.Crear(
                "PROD-002",
                "Mouse inalámbrico",
                1,
                20.00m,
                0m,
                new List<Impuesto>
                {
                    Impuesto.Crear("2", "4", 15.00m, 20.00m) // IVA 15% sobre 20 = 3.00
                })
        };

        var notaCredito = NotaCredito.Crear(
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
            motivo: "Devolución de mercadería por defecto",
            detalles: detalles
        );

        notaCredito.TenantId.ShouldBe(tenantId);
        notaCredito.CodDoc.ShouldBe("04");
        notaCredito.CodDocModificado.ShouldBe("01");
        notaCredito.NumDocModificado.ShouldBe("001-001-000000100");
        notaCredito.Motivo.ShouldBe("Devolución de mercadería por defecto");
        notaCredito.TotalSinImpuestos.ShouldBe(120.00m);
        notaCredito.ValorModificacion.ShouldBe(138.00m);
        notaCredito.Estado.ShouldBe("CREADA");
        notaCredito.Detalles.Count.ShouldBe(2);
    }

    [Test]
    public void Crear_SinDetalles_DebeLanzarArgumentException()
    {
        Should.Throw<ArgumentException>(() =>
            NotaCredito.Crear(
                tenantId: Guid.NewGuid(),
                ambiente: 1,
                razonSocial: "EMISOR",
                rucEmisor: "0957790108001",
                establecimiento: "001",
                puntoEmision: "001",
                secuencial: "000000001",
                direccionMatriz: "Quito",
                fechaEmision: DateTime.UtcNow.Date,
                cliente: _clienteDefault,
                numDocModificado: "001-001-000000100",
                fechaEmisionDocSustento: DateTime.UtcNow.Date,
                motivo: "Devolución",
                detalles: new List<DetalleNotaCredito>()
            )
        );
    }

    [Test]
    public void Emisor_SecuencialNotaCredito_DebeIncrementarCorrectamente()
    {
        var emisor = Emisor.Crear(
            tenantId: Guid.NewGuid(),
            ruc: "0957790108001",
            razonSocial: "EMISOR S.A.",
            direccionMatriz: "Quito",
            codigoEstablecimiento: "001",
            puntoEmision: "001",
            secuencialInicial: 0
        );

        emisor.SecuencialNotaCredito.ShouldBe(0);

        var primerSecuencial = emisor.ObtenerSiguienteSecuencialNotaCredito();
        primerSecuencial.ShouldBe("000000001");
        emisor.SecuencialNotaCredito.ShouldBe(2);

        var segundoSecuencial = emisor.ObtenerSiguienteSecuencialNotaCredito();
        segundoSecuencial.ShouldBe("000000002");
        emisor.SecuencialNotaCredito.ShouldBe(3);
    }
}

