using BillingSaaS.Domain.Entities;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Domain.UnitTests.Entities;

[TestFixture]
public class MigrationEntitiesTests
{
    [Test]
    public void TenantSubscription_MarcarComoVencido_DebeCambiarEstadoAVencido()
    {
        var tenantId = Guid.NewGuid();
        var sub = TenantSubscription.Crear(
            tenantId: tenantId,
            planId: 1,
            fechaInicio: DateTime.UtcNow.AddMonths(-6),
            fechaVencimiento: DateTime.UtcNow.AddMonths(-1),
            frecuencia: "ANUAL",
            diasGracia: 3);

        sub.Estado.ShouldBe("ACTIVO");

        sub.MarcarComoVencido();

        sub.Estado.ShouldBe("VENCIDO");
    }

    [Test]
    public void Factura_CrearMigrada_PermiteOmitirInvarianteConsumidorFinalMayorA50()
    {
        var tenantId = Guid.NewGuid();
        var consumidorFinal = Comprador.Crear("07", "9999999999999", "CONSUMIDOR FINAL");
        var detalle = DetalleFactura.Crear(
            codigoPrincipal: "PROD-01",
            descripcion: "Producto Historico",
            cantidad: 2,
            precioUnitario: 50.00m,
            descuento: 0,
            impuestos: [Impuesto.Crear("2", "4", 15.00m, 100.00m)]);

        // Sin validar invariantes (histórico), no debe lanzar excepción aunque total supere $50 USD
        var factura = Factura.Crear(
            tenantId: tenantId,
            ambiente: 2,
            razonSocial: "Empresa Prueba",
            rucEmisor: "1790000000001",
            establecimiento: "001",
            puntoEmision: "001",
            secuencial: "000000123",
            direccionMatriz: "Quito",
            fechaEmision: DateTime.UtcNow.AddYears(-2),
            cliente: consumidorFinal,
            detalles: [detalle],
            validarInvariantes: false);

        factura.ImporteTotal.ShouldBe(115.00m);
        factura.Cliente.EsConsumidorFinal().ShouldBeTrue();

        factura.EstablecerEstadoHistorico("AUTORIZADO", "12345678901234567890", DateTime.UtcNow.AddYears(-2));
        factura.Estado.ShouldBe("AUTORIZADO");
        factura.NumeroAutorizacion.ShouldBe("12345678901234567890");
    }

    [Test]
    public void Factura_Crear_ConValidarInvariantesActivo_LanzaExcepcionParaConsumidorFinalMayorA50()
    {
        var tenantId = Guid.NewGuid();
        var consumidorFinal = Comprador.Crear("07", "9999999999999", "CONSUMIDOR FINAL");
        var detalle = DetalleFactura.Crear(
            codigoPrincipal: "PROD-01",
            descripcion: "Producto Actual",
            cantidad: 2,
            precioUnitario: 50.00m,
            descuento: 0,
            impuestos: [Impuesto.Crear("2", "4", 15.00m, 100.00m)]);

        Should.Throw<InvalidOperationException>(() =>
        {
            Factura.Crear(
                tenantId: tenantId,
                ambiente: 2,
                razonSocial: "Empresa Prueba",
                rucEmisor: "1790000000001",
                establecimiento: "001",
                puntoEmision: "001",
                secuencial: "000000123",
                direccionMatriz: "Quito",
                fechaEmision: DateTime.UtcNow,
                cliente: consumidorFinal,
                detalles: [detalle],
                validarInvariantes: true);
        });
    }
}
