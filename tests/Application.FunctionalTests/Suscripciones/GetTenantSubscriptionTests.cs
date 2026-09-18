using System;
using System.Threading.Tasks;
using BillingSaaS.Application.FunctionalTests.Infrastructure;
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
        planes.Count.ShouldBeGreaterThanOrEqualTo(3);
        planes.ShouldContain(p => p.Codigo == "EMPRENDEDOR");
        planes.ShouldContain(p => p.Codigo == "COMERCIO_PRO");
        planes.ShouldContain(p => p.Codigo == "PYME_MULTI");
        // El plan LEGACY es privado y no debe figurar en el catálogo público
        planes.ShouldNotContain(p => p.Codigo == "LEGACY");
    }

    [Test]
    public async Task GetTenantSubscription_ConSuscripcion_DebeRetornarEstadoYMetricas()
    {
        var tenantId = Guid.NewGuid();
        await TestApp.RunAsUserAsync("tenant_test@local", "Password123!", [], tenantId);

        // Obtener un plan existente
        var planes = await TestApp.SendAsync(new GetPlanesQuery());
        var plan = planes.First(p => p.Codigo == "COMERCIO_PRO");

        var hoy = DateTime.UtcNow;
        var sub = TenantSubscription.Crear(
            tenantId,
            plan.Id,
            hoy.AddDays(-5),
            hoy.AddDays(25),
            "MENSUAL"
        );
        await TestApp.AddAsync(sub);

        var query = new GetTenantSubscriptionQuery();
        var result = await TestApp.SendAsync(query);

        result.ShouldNotBeNull();
        result.PlanCodigo.ShouldBe("COMERCIO_PRO");
        result.PlanNombre.ShouldBe("Comercio Pro");
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
}

