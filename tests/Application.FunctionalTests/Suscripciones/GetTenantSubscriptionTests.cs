using System;
using System.Linq;
using System.Threading.Tasks;
using BillingSaaS.Application.FunctionalTests.Infrastructure;
using BillingSaaS.Application.Suscripciones.Commands.RenovarSuscripcion;
using BillingSaaS.Application.Suscripciones.Queries.GetPlanes;
using BillingSaaS.Application.Suscripciones.Queries.GetTenantSubscription;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Domain.Exceptions;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.FunctionalTests.Suscripciones;

public class GetTenantSubscriptionTests : TestBase
{
    [Test]
    public async Task GetPlanes_DebeRetornarCatalogoDePlanesPublicos()
    {
        var planes = await TestApp.SendAsync(new GetPlanesQuery());

        planes.ShouldNotBeNull();
        planes.Count.ShouldBe(2);
        planes.ShouldContain(p => p.Codigo == "MIGRACION_SISTEMA");
        planes.ShouldContain(p => p.Codigo == "MIGRACION_FIRMA");
    }

    [Test]
    public async Task GetTenantSubscription_ConSuscripcion_DebeRetornarEstadoYMetricas()
    {
        var tenantId = Guid.NewGuid();
        await TestApp.RunAsUserAsync("tenant_test@local", "Password123!", [], tenantId);

        // Obtener un plan existente
        var planes = await TestApp.SendAsync(new GetPlanesQuery());
        var plan = planes.First(p => p.Codigo == "MIGRACION_SISTEMA");

        var hoy = DateTime.UtcNow;
        var sub = TenantSubscription.Crear(
            tenantId,
            plan.Id,
            hoy.AddDays(-5),
            hoy.AddDays(25),
            "ANUAL"
        );
        await TestApp.AddAsync(sub);

        var query = new GetTenantSubscriptionQuery();
        var result = await TestApp.SendAsync(query);

        result.ShouldNotBeNull();
        result.PlanCodigo.ShouldBe("MIGRACION_SISTEMA");
        result.PlanNombre.ShouldBe("Plan Migración (Solo Sistema)");
        result.EsIlimitado.ShouldBeTrue();
        result.Estado.ShouldBe("ACTIVO");
        result.DiasRestantes.ShouldBeGreaterThan(0);
        result.EnPeriodoGracia.ShouldBeFalse();
        result.DocumentosEmitidos.ShouldBe(0);
    }

    [Test]
    public async Task GetTenantSubscription_SinSuscripcion_DebeLanzarSubscriptionRequiredException()
    {
        var tenantId = Guid.NewGuid();
        await TestApp.RunAsUserAsync("no_sub@local", "Password123!", [], tenantId);

        await Should.ThrowAsync<SubscriptionRequiredException>(() =>
            TestApp.SendAsync(new GetTenantSubscriptionQuery()));
    }

    [Test]
    public async Task RenovarSuscripcion_UpgradePlan_DebeActualizarSuscripcionCorrectamente()
    {
        var tenantId = Guid.NewGuid();
        await TestApp.RunAsUserAsync("upgrade_test@local", "Password123!", [], tenantId);

        var planes = await TestApp.SendAsync(new GetPlanesQuery());
        var planInicial = planes.First(p => p.Codigo == "MIGRACION_SISTEMA");
        var planNuevo = planes.First(p => p.Codigo == "MIGRACION_FIRMA");

        var hoy = DateTime.UtcNow;
        var sub = TenantSubscription.Crear(
            tenantId,
            planInicial.Id,
            hoy.AddDays(-20),
            hoy.AddDays(10),
            "ANUAL"
        );
        await TestApp.AddAsync(sub);

        var renovarResult = await TestApp.SendAsync(new RenovarSuscripcionCommand
        {
            PlanId = planNuevo.Id,
            Frecuencia = "ANUAL"
        });

        renovarResult.ShouldNotBeNull();
        renovarResult.PlanCodigo.ShouldBe("MIGRACION_FIRMA");
        renovarResult.PlanNombre.ShouldBe("Plan Migración + Firma Digital");
        renovarResult.EsIlimitado.ShouldBeTrue();
        renovarResult.Estado.ShouldBe("ACTIVO");
        renovarResult.DiasRestantes.ShouldBeGreaterThan(300);
    }
}

