using BillingSaaS.Application.Facturas.Queries.GetFacturas;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Domain.ValueObjects;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.FunctionalTests.Facturas.Queries;

public class GetFacturasTests : TestBase
{
    [Test]
    public async Task GetFacturas_SinFiltros_DebeRetornarListaPaginada()
    {
        await TestApp.RunAsDefaultUserAsync();

        var emisor = Emisor.Crear(Guid.NewGuid(), "0957790108001", "FARMACIA SAN JOSE", "Guayaquil");
        await TestApp.AddAsync(emisor);

        var comprador1 = Comprador.Crear("07", "CONSUMIDOR FINAL", "9999999999999", "Guayaquil", null);
        var detalle1 = DetalleFactura.Crear("P01", "Paracetamol", 1, 10m, 0, [Impuesto.Crear("2", "4", 15m, 10m)]);

        var factura1 = Factura.Crear(
            emisor.TenantId, 1, emisor.RazonSocial, emisor.Ruc,
            "001", "001", "000000001", emisor.DireccionMatriz,
            DateTime.Now.AddDays(-1), comprador1, [detalle1], emisor.Id);
        factura1.AsignarClaveAcceso(new string('1', 49));

        var comprador2 = Comprador.Crear("07", "CONSUMIDOR FINAL", "9999999999999", "Guayaquil", null);
        var detalle2 = DetalleFactura.Crear("P01", "Paracetamol", 1, 10m, 0, [Impuesto.Crear("2", "4", 15m, 10m)]);

        var factura2 = Factura.Crear(
            emisor.TenantId, 1, emisor.RazonSocial, emisor.Ruc,
            "001", "001", "000000002", emisor.DireccionMatriz,
            DateTime.Now, comprador2, [detalle2], emisor.Id);
        factura2.AsignarClaveAcceso(new string('2', 49));

        await TestApp.AddAsync(factura1);
        await TestApp.AddAsync(factura2);

        var query = new GetFacturasQuery { EmisorId = emisor.Id, PageNumber = 1, PageSize = 10 };
        var result = await TestApp.SendAsync(query);

        result.ShouldNotBeNull();
        result.TotalCount.ShouldBe(2);
        result.Items.Count.ShouldBe(2);
        result.Items.First().Secuencial.ShouldBe("000000002"); // Ordenado descendente por fecha
    }

    [Test]
    public async Task GetFacturas_FiltrarPorEstado_DebeRetornarSoloCoincidentes()
    {
        await TestApp.RunAsDefaultUserAsync();

        var emisor = Emisor.Crear(Guid.NewGuid(), "0957790108002", "SUPERMERCADO CENTRAL", "Quito");
        await TestApp.AddAsync(emisor);

        var comprador1 = Comprador.Crear("07", "CONSUMIDOR FINAL", "9999999999999", "Quito", null);
        var detalle1 = DetalleFactura.Crear("P02", "Agua", 1, 1m, 0, [Impuesto.Crear("2", "4", 15m, 1m)]);

        var facturaCreada = Factura.Crear(
            emisor.TenantId, 1, emisor.RazonSocial, emisor.Ruc,
            "001", "001", "000000010", emisor.DireccionMatriz,
            DateTime.Now, comprador1, [detalle1], emisor.Id);
        facturaCreada.AsignarClaveAcceso(new string('3', 49));

        var comprador2 = Comprador.Crear("07", "CONSUMIDOR FINAL", "9999999999999", "Quito", null);
        var detalle2 = DetalleFactura.Crear("P02", "Agua", 1, 1m, 0, [Impuesto.Crear("2", "4", 15m, 1m)]);

        var facturaAutorizada = Factura.Crear(
            emisor.TenantId, 1, emisor.RazonSocial, emisor.Ruc,
            "001", "001", "000000011", emisor.DireccionMatriz,
            DateTime.Now, comprador2, [detalle2], emisor.Id);
        facturaAutorizada.AsignarClaveAcceso(new string('4', 49));
        facturaAutorizada.MarcarComoAutorizada("1234567890123456789012345678901234567890123456789", DateTime.Now);

        await TestApp.AddAsync(facturaCreada);
        await TestApp.AddAsync(facturaAutorizada);

        var query = new GetFacturasQuery { EmisorId = emisor.Id, Estado = "AUTORIZADO" };
        var result = await TestApp.SendAsync(query);

        result.Items.Count.ShouldBe(1);
        result.Items.First().Secuencial.ShouldBe("000000011");
        result.Items.First().Estado.ShouldBe("AUTORIZADO");
        result.Items.First().NumeroAutorizacion.ShouldBe("1234567890123456789012345678901234567890123456789");
    }
}
