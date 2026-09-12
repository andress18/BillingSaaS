using BillingSaaS.Application.Common.Exceptions;
using BillingSaaS.Application.Facturas.Commands.EmitirFactura;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.UnitTests.Facturas.Commands;

[TestFixture]
public class EmitirFacturaCommandValidatorTests
{
    private EmitirFacturaCommandValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new EmitirFacturaCommandValidator();
    }

    [Test]
    public void Validar_ComandoCorrecto_DebeSerValido()
    {
        var command = new EmitirFacturaCommand
        {
            EmisorId = 1,
            Cliente = new EmitirFacturaCommand.CompradorDto(
                TipoIdentificacion: "07",
                Identificacion: "9999999999999",
                RazonSocial: "CONSUMIDOR FINAL",
                Direccion: "Quito",
                CorreoElectronico: "test@example.com"
            ),
            Detalles =
            [
                new EmitirFacturaCommand.DetalleDto(
                    CodigoPrincipal: "PROD-01",
                    Descripcion: "Paracetamol 500mg",
                    Cantidad: 2,
                    PrecioUnitario: 1.50m,
                    Descuento: 0,
                    Impuestos: [new EmitirFacturaCommand.ImpuestoDto("2", "4", 15m, 3.00m)]
                )
            ]
        };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeTrue();
    }

    [Test]
    public void Validar_EmisorIdInvalido_DebeFallar()
    {
        var command = new EmitirFacturaCommand
        {
            EmisorId = 0,
            Cliente = new EmitirFacturaCommand.CompradorDto("07", "9999999999999", "CONSUMIDOR FINAL", null, null),
            Detalles =
            [
                new EmitirFacturaCommand.DetalleDto("PROD-01", "Paracetamol", 1, 1m, 0, [new EmitirFacturaCommand.ImpuestoDto("2", "4", 15m, 1m)])
            ]
        };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(command.EmisorId));
    }

    [Test]
    public void Validar_SinDetalles_DebeFallar()
    {
        var command = new EmitirFacturaCommand
        {
            EmisorId = 1,
            Cliente = new EmitirFacturaCommand.CompradorDto("07", "9999999999999", "CONSUMIDOR FINAL", null, null),
            Detalles = []
        };

        var result = _validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(command.Detalles));
    }
}
