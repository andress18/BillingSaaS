using BillingSaaS.Domain.Constants;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Domain.UnitTests.Constants;

[TestFixture]
public class RegimenRimpeTiposTests
{
    [TestCase("CONTRIBUYENTE RÉGIMEN RIMPE", RegimenRimpeTipos.Emprendedor)]
    [TestCase("RIMPE_EMPRENDEDOR", RegimenRimpeTipos.Emprendedor)]
    [TestCase("EMPRENDEDOR", RegimenRimpeTipos.Emprendedor)]
    [TestCase("rimpe emprendedor", RegimenRimpeTipos.Emprendedor)]
    [TestCase("CONTRIBUYENTE NEGOCIO POPULAR - RÉGIMEN RIMPE", RegimenRimpeTipos.NegocioPopular)]
    [TestCase("RIMPE_NEGOCIO_POPULAR", RegimenRimpeTipos.NegocioPopular)]
    [TestCase("NEGOCIO_POPULAR", RegimenRimpeTipos.NegocioPopular)]
    [TestCase("negocio popular", RegimenRimpeTipos.NegocioPopular)]
    [TestCase("GENERAL", null)]
    [TestCase("general", null)]
    [TestCase("NINGUNO", null)]
    [TestCase("NO APLICA", null)]
    [TestCase("", null)]
    [TestCase("   ", null)]
    [TestCase(null, null)]
    public void Normalizar_DeberiaMapearCorrectamente(string? input, string? expected)
    {
        var result = RegimenRimpeTipos.Normalizar(input);
        result.ShouldBe(expected);
    }

    [Test]
    public void EsValido_ConOpcionesOficialesSRI_DeberiaRetornarTrue()
    {
        RegimenRimpeTipos.EsValido(RegimenRimpeTipos.Emprendedor).ShouldBeTrue();
        RegimenRimpeTipos.EsValido(RegimenRimpeTipos.NegocioPopular).ShouldBeTrue();
        RegimenRimpeTipos.EsValido("RIMPE_EMPRENDEDOR").ShouldBeTrue();
        RegimenRimpeTipos.EsValido("RIMPE_NEGOCIO_POPULAR").ShouldBeTrue();
        RegimenRimpeTipos.EsValido("GENERAL").ShouldBeTrue();
        RegimenRimpeTipos.EsValido(null).ShouldBeTrue();
        RegimenRimpeTipos.EsValido("").ShouldBeTrue();
    }
}

