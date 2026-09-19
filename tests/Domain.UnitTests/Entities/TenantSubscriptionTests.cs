using System;
using BillingSaaS.Domain.Entities;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Domain.UnitTests.Entities;

[TestFixture]
public class TenantSubscriptionTests
{
    private Plan _plan;

    [SetUp]
    public void SetUp()
    {
        _plan = Plan.Crear(
            codigo: "COMERCIO_PRO",
            nombre: "Comercio Pro",
            descripcion: "Ilimitado",
            precioMensual: 10m,
            precioAnual: 99m,
            maxDocumentosMensuales: null,
            maxDocumentosAnuales: null,
            maxEstablecimientos: 1,
            tiposDocumentosPermitidos: "01,04,05",
            esPublico: true
        );
    }

    [Test]
    public void EstaVigente_SuscripcionActivaDentroDePlazo_DebeRetornarTrue()
    {
        var hoy = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
        var sub = TenantSubscription.Crear(
            tenantId: Guid.NewGuid(),
            planId: 1,
            frecuencia: "MENSUAL",
            fechaInicio: hoy.AddDays(-10),
            fechaVencimiento: hoy.AddDays(20)
        );

        sub.EstaVigente(hoy).ShouldBeTrue();
        sub.EstaEnPeriodoGracia(hoy).ShouldBeFalse();
        sub.ObtenerDiasRestantes(hoy).ShouldBe(20);
    }

    [Test]
    public void EstaVigente_DentroDePeriodoGracia3Dias_DebeRetornarTrueYMarcarGracia()
    {
        var hoy = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
        // Venció hace 2 días (gracia es de hasta 3 días)
        var sub = TenantSubscription.Crear(
            tenantId: Guid.NewGuid(),
            planId: 1,
            frecuencia: "MENSUAL",
            fechaInicio: hoy.AddMonths(-1),
            fechaVencimiento: hoy.AddDays(-2)
        );

        sub.EstaVigente(hoy).ShouldBeTrue();
        sub.EstaEnPeriodoGracia(hoy).ShouldBeTrue();
        sub.ObtenerDiasRestantes(hoy).ShouldBe(0);
    }

    [Test]
    public void EstaVigente_VencidaMasDe3Dias_DebeRetornarFalse()
    {
        var hoy = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
        // Venció hace 4 días (fuera de periodo de gracia)
        var sub = TenantSubscription.Crear(
            tenantId: Guid.NewGuid(),
            planId: 1,
            frecuencia: "MENSUAL",
            fechaInicio: hoy.AddMonths(-1),
            fechaVencimiento: hoy.AddDays(-4)
        );

        sub.EstaVigente(hoy).ShouldBeFalse();
        sub.EstaEnPeriodoGracia(hoy).ShouldBeFalse();
    }

    [Test]
    public void ObtenerInicioCicloActual_Mensual_DebeCalcularFechaDeCorteDelMesCorriente()
    {
        var hoy = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        // Inició el 5 de cada mes
        var sub = TenantSubscription.Crear(
            tenantId: Guid.NewGuid(),
            planId: 1,
            frecuencia: "MENSUAL",
            fechaInicio: new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc),
            fechaVencimiento: new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc)
        );

        var inicioCiclo = sub.ObtenerInicioCicloActual(hoy);

        // El ciclo actual arrancó el 5 de septiembre de 2026
        inicioCiclo.ShouldBe(new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void Renovar_DebeActualizarVencimientoCorrectamente()
    {
        var inicio = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var vencimiento = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        var sub = TenantSubscription.Crear(
            tenantId: Guid.NewGuid(),
            planId: 1,
            frecuencia: "MENSUAL",
            fechaInicio: inicio,
            fechaVencimiento: vencimiento
        );

        var nuevoVencimiento = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        sub.Renovar(nuevoVencimiento);

        sub.FechaVencimiento.ShouldBe(nuevoVencimiento);
        sub.Estado.ShouldBe("ACTIVO");
    }
}
