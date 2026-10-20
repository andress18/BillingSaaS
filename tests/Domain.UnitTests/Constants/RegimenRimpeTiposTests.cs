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
    [TestCase("GENERAL", RegimenRimpeTipos.General)]
    [TestCase("general", RegimenRimpeTipos.General)]
    [TestCase("CONTRIBUYENTE RÉGIMEN GENERAL", RegimenRimpeTipos.General)]
    [TestCase("CONTRIBUYENTE REGIMEN GENERAL", RegimenRimpeTipos.General)]
    [TestCase("RÉGIMEN GENERAL", RegimenRimpeTipos.General)]
    [TestCase("NINGUNO", RegimenRimpeTipos.General)]
    [TestCase("NO APLICA", RegimenRimpeTipos.General)]
    [TestCase("", null)]
    [TestCase("   ", null)]
    [TestCase(null, null)]
    public void Normalizar_DeberiaMapearCorrectamente(string? input, string? expected)
    {
        var result = RegimenRimpeTipos.Normalizar(input);
        result.ShouldBe(expected);
    }

    [TestCase(null, RegimenRimpeTipos.General)]
    [TestCase("", RegimenRimpeTipos.General)]
    [TestCase("   ", RegimenRimpeTipos.General)]
    [TestCase("GENERAL", RegimenRimpeTipos.General)]
    [TestCase("general", RegimenRimpeTipos.General)]
    [TestCase("CONTRIBUYENTE RÉGIMEN GENERAL", RegimenRimpeTipos.General)]
    [TestCase("CONTRIBUYENTE RÉGIMEN RIMPE", RegimenRimpeTipos.Emprendedor)]
    [TestCase("RIMPE_EMPRENDEDOR", RegimenRimpeTipos.Emprendedor)]
    [TestCase("CONTRIBUYENTE NEGOCIO POPULAR - RÉGIMEN RIMPE", RegimenRimpeTipos.NegocioPopular)]
    [TestCase("RIMPE_NEGOCIO_POPULAR", RegimenRimpeTipos.NegocioPopular)]
    public void ResolverParaRide_DeberiaResolverCorrectamente(string? input, string expected)
    {
        var result = RegimenRimpeTipos.ResolverParaRide(input);
        result.ShouldBe(expected);
    }

    [TestCase("CONTRIBUYENTE RÉGIMEN RIMPE", true)]
    [TestCase("RIMPE_EMPRENDEDOR", true)]
    [TestCase("CONTRIBUYENTE NEGOCIO POPULAR - RÉGIMEN RIMPE", true)]
    [TestCase("RIMPE_NEGOCIO_POPULAR", true)]
    [TestCase("CONTRIBUYENTE RÉGIMEN GENERAL", false)]
    [TestCase("GENERAL", false)]
    [TestCase(null, false)]
    [TestCase("", false)]
    public void EsRimpe_DeberiaDetectarSoloRegimenesRimpe(string? input, bool expected)
    {
        var result = RegimenRimpeTipos.EsRimpe(input);
        result.ShouldBe(expected);
    }

    [Test]
    public void EsValido_ConOpcionesOficialesSRI_DeberiaRetornarTrue()
    {
        RegimenRimpeTipos.EsValido(RegimenRimpeTipos.General).ShouldBeTrue();
        RegimenRimpeTipos.EsValido(RegimenRimpeTipos.Emprendedor).ShouldBeTrue();
        RegimenRimpeTipos.EsValido(RegimenRimpeTipos.NegocioPopular).ShouldBeTrue();
        RegimenRimpeTipos.EsValido("RIMPE_EMPRENDEDOR").ShouldBeTrue();
        RegimenRimpeTipos.EsValido("RIMPE_NEGOCIO_POPULAR").ShouldBeTrue();
        RegimenRimpeTipos.EsValido("GENERAL").ShouldBeTrue();
        RegimenRimpeTipos.EsValido(null).ShouldBeTrue();
        RegimenRimpeTipos.EsValido("").ShouldBeTrue();
    }
}

