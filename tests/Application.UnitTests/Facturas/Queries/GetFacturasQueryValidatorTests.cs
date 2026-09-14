using BillingSaaS.Application.Facturas.Queries.GetFacturas;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.UnitTests.Facturas.Queries;

[TestFixture]
public class GetFacturasQueryValidatorTests
{
    private GetFacturasQueryValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new GetFacturasQueryValidator();
    }

    [Test]
    public void Validar_ParametrosPorDefecto_DebeSerValido()
    {
        var query = new GetFacturasQuery();

        var result = _validator.Validate(query);

        result.IsValid.ShouldBeTrue();
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Validar_PageNumberInvalido_DebeFallar(int pageNumber)
    {
        var query = new GetFacturasQuery { PageNumber = pageNumber };

        var result = _validator.Validate(query);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(query.PageNumber));
    }

    [TestCase(0)]
    [TestCase(-5)]
    [TestCase(101)]
    public void Validar_PageSizeInvalido_DebeFallar(int pageSize)
    {
        var query = new GetFacturasQuery { PageSize = pageSize };

        var result = _validator.Validate(query);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(query.PageSize));
    }
}

