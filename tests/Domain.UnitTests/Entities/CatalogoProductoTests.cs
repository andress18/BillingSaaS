using BillingSaaS.Domain.Entities;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Domain.UnitTests.Entities;

[TestFixture]
public class CatalogoProductoTests
{
    [Test]
    public void Crear_ConDatosValidos_DebeCrearCatalogoProducto()
    {
        var tenantId = Guid.NewGuid();
        var producto = CatalogoProducto.Crear(
            tenantId,
            "PROD-001",
            "Servicio de Consultoria",
            120.5000m,
            "2",
            "4",
            15.00m);

        producto.TenantId.ShouldBe(tenantId);
        producto.CodigoPrincipal.ShouldBe("PROD-001");
        producto.Descripcion.ShouldBe("Servicio de Consultoria");
        producto.PrecioUnitario.ShouldBe(120.5000m);
        producto.CodigoImpuesto.ShouldBe("2");
        producto.CodigoPorcentaje.ShouldBe("4");
        producto.Tarifa.ShouldBe(15.00m);
        producto.Activo.ShouldBeTrue();
    }

    [Test]
    public void Crear_ConPrecioNegativo_DebeLanzarArgumentException()
    {
        Should.Throw<ArgumentException>(() =>
            CatalogoProducto.Crear(
                Guid.NewGuid(),
                "PROD-002",
                "Producto Invalido",
                -10m));
    }

    [Test]
    public void ActualizarDatos_DebeActualizarCampos()
    {
        var producto = CatalogoProducto.Crear(
            Guid.NewGuid(),
            "PROD-001",
            "Producto Inicial",
            50m);

        producto.ActualizarDatos("Producto Modificado", 65.2500m, "2", "0", 0m);

        producto.Descripcion.ShouldBe("Producto Modificado");
        producto.PrecioUnitario.ShouldBe(65.2500m);
        producto.CodigoPorcentaje.ShouldBe("0");
        producto.Tarifa.ShouldBe(0m);
    }

    [Test]
    public void DesactivarYActivar_DebeCambiarEstado()
    {
        var producto = CatalogoProducto.Crear(Guid.NewGuid(), "P-01", "Item", 10m);

        producto.Desactivar();
        producto.Activo.ShouldBeFalse();

        producto.Activar();
        producto.Activo.ShouldBeTrue();
    }
}

