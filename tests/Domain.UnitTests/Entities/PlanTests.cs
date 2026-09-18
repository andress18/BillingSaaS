using System;
using BillingSaaS.Domain.Entities;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Domain.UnitTests.Entities;

[TestFixture]
public class PlanTests
{
    [Test]
    public void Crear_ConDatosValidos_DebeCrearPlanCorrectamente()
    {
        var plan = Plan.Crear(
            codigo: "EMPRENDEDOR",
            nombre: "Emprendedor",
            descripcion: "Plan para profesionales independientes",
            precioMensual: 5.00m,
            precioAnual: 50.00m,
            maxDocumentosMensuales: 30,
            maxDocumentosAnuales: 360,
            maxEstablecimientos: 1,
            tiposDocumentosPermitidos: "01,04",
            esPublico: true
        );

        plan.Codigo.ShouldBe("EMPRENDEDOR");
        plan.Nombre.ShouldBe("Emprendedor");
        plan.PrecioMensual.ShouldBe(5.00m);
        plan.PrecioAnual.ShouldBe(50.00m);
        plan.MaxDocumentosMensuales.ShouldBe(30);
        plan.MaxDocumentosAnuales.ShouldBe(360);
        plan.MaxEstablecimientos.ShouldBe(1);
        plan.EsPublico.ShouldBeTrue();
        plan.Activo.ShouldBeTrue();
    }

    [Test]
    public void PermiteTipoDocumento_DebeValidarCorrectamente()
    {
        var plan = Plan.Crear(
            codigo: "EMPRENDEDOR",
            nombre: "Emprendedor",
            descripcion: "Plan básico",
            precioMensual: 5m,
            precioAnual: 50m,
            maxDocumentosMensuales: 30,
            maxDocumentosAnuales: 360,
            maxEstablecimientos: 1,
            tiposDocumentosPermitidos: "01,04",
            esPublico: true
        );

        plan.PermiteTipoDocumento("01").ShouldBeTrue(); // Factura
        plan.PermiteTipoDocumento("04").ShouldBeTrue(); // Nota de Crédito
        plan.PermiteTipoDocumento("05").ShouldBeFalse(); // Nota de Débito
        plan.PermiteTipoDocumento("06").ShouldBeFalse(); // Guía de Remisión
    }

    [Test]
    public void EsIlimitado_DebeDistinguirPlanesIlimitados()
    {
        var planIlimitado = Plan.Crear(
            codigo: "COMERCIO_PRO",
            nombre: "Comercio Pro",
            descripcion: "Facturación ilimitada",
            precioMensual: 10m,
            precioAnual: 99m,
            maxDocumentosMensuales: null,
            maxDocumentosAnuales: null,
            maxEstablecimientos: 1,
            tiposDocumentosPermitidos: "01,04,05",
            esPublico: true
        );

        var planLimitado = Plan.Crear(
            codigo: "EMPRENDEDOR",
            nombre: "Emprendedor",
            descripcion: "30 facturas al mes",
            precioMensual: 5m,
            precioAnual: 50m,
            maxDocumentosMensuales: 30,
            maxDocumentosAnuales: 360,
            maxEstablecimientos: 1,
            tiposDocumentosPermitidos: "01,04",
            esPublico: true
        );

        planIlimitado.EsIlimitado("MENSUAL").ShouldBeTrue();
        planIlimitado.EsIlimitado("ANUAL").ShouldBeTrue();

        planLimitado.EsIlimitado("MENSUAL").ShouldBeFalse();
        planLimitado.EsIlimitado("ANUAL").ShouldBeFalse();
        planLimitado.ObtenerLimiteDocumentos("MENSUAL").ShouldBe(30);
        planLimitado.ObtenerLimiteDocumentos("ANUAL").ShouldBe(360);
    }
}

