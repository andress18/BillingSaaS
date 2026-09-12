using BillingSaaS.Domain.Entities;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Domain.UnitTests.Entities;

[TestFixture]
public class EmisorTests
{
    [Test]
    public void Crear_ConDatosValidos_DebeInstanciarEmisor()
    {
        var tenantId = Guid.NewGuid();
        var emisor = Emisor.Crear(
            tenantId: tenantId,
            ruc: "0957790108001",
            razonSocial: "FARMACIA SAN JOSE CIA LTDA",
            direccionMatriz: "Av. Amazonas y Naciones Unidas",
            codigoEstablecimiento: "001",
            puntoEmision: "002",
            ambiente: 1,
            obligadoContabilidad: true,
            regimenRimpe: "CONTRIBUYENTE RÉGIMEN RIMPE"
        );

        emisor.TenantId.ShouldBe(tenantId);
        emisor.Ruc.ShouldBe("0957790108001");
        emisor.RazonSocial.ShouldBe("FARMACIA SAN JOSE CIA LTDA");
        emisor.CodigoEstablecimiento.ShouldBe("001");
        emisor.PuntoEmision.ShouldBe("002");
        emisor.Ambiente.ShouldBe(1);
        emisor.ObligadoContabilidad.ShouldBeTrue();
        emisor.Activo.ShouldBeTrue();
        emisor.SecuencialFactura.ShouldBe(0);
    }

    [Test]
    public void ObtenerSiguienteSecuencialFactura_DebeIncrementarSecuencialFormateado9Digitos()
    {
        var emisor = Emisor.Crear(
            tenantId: Guid.NewGuid(),
            ruc: "0957790108001",
            razonSocial: "FARMACIA SAN JOSE CIA LTDA",
            direccionMatriz: "Av. Amazonas",
            secuencialInicial: 42
        );

        var sec1 = emisor.ObtenerSiguienteSecuencialFactura();
        var sec2 = emisor.ObtenerSiguienteSecuencialFactura();

        sec1.ShouldBe("000000043");
        sec2.ShouldBe("000000044");
        emisor.SecuencialFactura.ShouldBe(44);
    }

    [Test]
    public void ConfigurarCertificado_ConDatosValidos_DebeActualizarCertificado()
    {
        var emisor = Emisor.Crear(
            tenantId: Guid.NewGuid(),
            ruc: "0957790108001",
            razonSocial: "FARMACIA SAN JOSE CIA LTDA",
            direccionMatriz: "Av. Amazonas"
        );

        byte[] fakeCert = [0x30, 0x82, 0x01];
        var fechaCaducidad = DateTime.UtcNow.AddYears(1);

        emisor.ConfigurarCertificado(fakeCert, "password123", fechaCaducidad, "CN=FARMACIA SAN JOSE");

        emisor.TieneCertificadoValido().ShouldBeTrue();
        emisor.SubjectCertificado.ShouldBe("CN=FARMACIA SAN JOSE");
        emisor.FechaCaducidadCertificado.ShouldBe(fechaCaducidad);
    }

    [Test]
    public void Crear_ConRucInvalido_DebeLanzarExcepcion()
    {
        Should.Throw<ArgumentException>(() =>
            Emisor.Crear(
                tenantId: Guid.NewGuid(),
                ruc: "12345", // Menos de 13 dígitos
                razonSocial: "Test",
                direccionMatriz: "Test"
            ));
    }
}
