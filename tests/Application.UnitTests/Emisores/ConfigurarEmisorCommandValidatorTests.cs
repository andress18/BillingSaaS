using BillingSaaS.Application.Emisores.Commands.ConfigurarEmisor;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.UnitTests.Emisores;

[TestFixture]
public class ConfigurarEmisorCommandValidatorTests
{
    private ConfigurarEmisorCommandValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new ConfigurarEmisorCommandValidator();
    }

    [Test]
    public void Validar_ConEstablecimientoYPuntoEmision001_DebeSerValido()
    {
        var command = new ConfigurarEmisorCommand
        {
            Ruc = "0957790108001",
            RazonSocial = "EMPRESA PRUEBA S.A.",
            DireccionMatriz = "Quito",
            CodigoEstablecimiento = "001",
            PuntoEmision = "001",
            Ambiente = 1
        };

        var result = _validator.Validate(command);
        result.IsValid.ShouldBeTrue();
    }

    [Test]
    public void Validar_ConEstablecimientoDistintoDe001_DebeFallar()
    {
        var command = new ConfigurarEmisorCommand
        {
            Ruc = "0957790108001",
            RazonSocial = "EMPRESA PRUEBA S.A.",
            DireccionMatriz = "Quito",
            CodigoEstablecimiento = "002",
            PuntoEmision = "001",
            Ambiente = 1
        };

        var result = _validator.Validate(command);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(command.CodigoEstablecimiento));
    }

    [Test]
    public void Validar_ConPuntoEmisionDistintoDe001_DebeFallar()
    {
        var command = new ConfigurarEmisorCommand
        {
            Ruc = "0957790108001",
            RazonSocial = "EMPRESA PRUEBA S.A.",
            DireccionMatriz = "Quito",
            CodigoEstablecimiento = "001",
            PuntoEmision = "002",
            Ambiente = 1
        };

        var result = _validator.Validate(command);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(command.PuntoEmision));
    }
}

