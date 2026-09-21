using BillingSaaS.Domain.Entities;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Domain.UnitTests.Entities;

[TestFixture]
public class CatalogoClienteTests
{
    [Test]
    public void Crear_ConDatosValidos_DebeCrearCatalogoCliente()
    {
        var tenantId = Guid.NewGuid();
        var cliente = CatalogoCliente.Crear(
            tenantId,
            "04",
            "1790012345001",
            "ACME CORP S.A.",
            "Av. 10 de Agosto",
            "contacto@acme.com");

        cliente.TenantId.ShouldBe(tenantId);
        cliente.TipoIdentificacion.ShouldBe("04");
        cliente.Identificacion.ShouldBe("1790012345001");
        cliente.RazonSocial.ShouldBe("ACME CORP S.A.");
        cliente.Direccion.ShouldBe("Av. 10 de Agosto");
        cliente.CorreoElectronico.ShouldBe("contacto@acme.com");
        cliente.Activo.ShouldBeTrue();
    }

    [Test]
    public void Crear_ConTenantVacio_DebeLanzarArgumentException()
    {
        Should.Throw<ArgumentException>(() =>
            CatalogoCliente.Crear(Guid.Empty, "04", "1790012345001", "ACME CORP"));
    }

    [Test]
    public void Crear_ConIdentificacionVacia_DebeLanzarArgumentException()
    {
        Should.Throw<ArgumentException>(() =>
            CatalogoCliente.Crear(Guid.NewGuid(), "04", "", "ACME CORP"));
    }

    [Test]
    public void ActualizarContacto_DebeModificarDatosYNormalizar()
    {
        var cliente = CatalogoCliente.Crear(
            Guid.NewGuid(),
            "05",
            "1712345678",
            "Juan Perez",
            "Quito",
            "juan@test.com");

        cliente.ActualizarContacto("Juan Perez Gomez", "Cumbaya", "juan.perez@empresa.com", "05");

        cliente.RazonSocial.ShouldBe("Juan Perez Gomez");
        cliente.Direccion.ShouldBe("Cumbaya");
        cliente.CorreoElectronico.ShouldBe("juan.perez@empresa.com");
    }

    [Test]
    public void DesactivarYActivar_DebeCambiarEstado()
    {
        var cliente = CatalogoCliente.Crear(Guid.NewGuid(), "05", "1712345678", "Juan Perez");

        cliente.Desactivar();
        cliente.Activo.ShouldBeFalse();

        cliente.Activar();
        cliente.Activo.ShouldBeTrue();
    }
}

