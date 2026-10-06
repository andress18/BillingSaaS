using BillingSaaS.MigrationTool.Models;
using BillingSaaS.MigrationTool.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Infrastructure.IntegrationTests;

[TestFixture]
public class MigrationToolServicesTests
{
    private SriXmlFacturaParser _parser = null!;
    private X509CertificateValidator _certValidator = null!;

    [SetUp]
    public void Setup()
    {
        _parser = new SriXmlFacturaParser(NullLogger<SriXmlFacturaParser>.Instance);
        _certValidator = new X509CertificateValidator(NullLogger<X509CertificateValidator>.Instance);
    }

    [Test]
    public void ParsearFacturaXml_XmlValido_DebeExtraerCabeceraTotalesCompradorYDetalles()
    {
        const string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<factura id=""comprobante"" version=""1.1.0"">
  <infoTributaria>
    <ambiente>2</ambiente>
    <tipoEmision>1</tipoEmision>
    <razonSocial>EMPRESA DEMO S.A.</razonSocial>
    <nombreComercial>DEMO STORE</nombreComercial>
    <ruc>0999999999001</ruc>
    <claveAcceso>0101202601099999999900120010010000000421234567812</claveAcceso>
    <codDoc>01</codDoc>
    <estab>001</estab>
    <ptoEmi>002</ptoEmi>
    <secuencial>000000042</secuencial>
    <dirMatriz>Av. 9 de Octubre y Malecón</dirMatriz>
    <contribuyenteRimpe>CONTRIBUYENTE RÉGIMEN RIMPE</contribuyenteRimpe>
  </infoTributaria>
  <infoFactura>
    <fechaEmision>15/02/2026</fechaEmision>
    <dirEstablecimiento>Sucursal 1</dirEstablecimiento>
    <obligadoContabilidad>NO</obligadoContabilidad>
    <tipoIdentificacionComprador>05</tipoIdentificacionComprador>
    <razonSocialComprador>JUAN PEREZ</razonSocialComprador>
    <identificacionComprador>0923456789</identificacionComprador>
    <totalSinImpuestos>80.00</totalSinImpuestos>
    <totalDescuento>5.00</totalDescuento>
    <totalConImpuestos>
      <totalImpuesto>
        <codigo>2</codigo>
        <codigoPorcentaje>4</codigoPorcentaje>
        <baseImponible>75.00</baseImponible>
        <valor>11.25</valor>
      </totalImpuesto>
    </totalConImpuestos>
    <importeTotal>86.25</importeTotal>
    <pagos>
      <pago>
        <formaPago>20</formaPago>
        <total>86.25</total>
      </pago>
    </pagos>
  </infoFactura>
  <detalles>
    <detalle>
      <codigoPrincipal>SERV-01</codigoPrincipal>
      <descripcion>Consultoría Técnica Especializada</descripcion>
      <cantidad>1.000000</cantidad>
      <precioUnitario>80.000000</precioUnitario>
      <descuento>5.00</descuento>
      <precioTotalSinImpuesto>75.00</precioTotalSinImpuesto>
      <impuestos>
        <impuesto>
          <codigo>2</codigo>
          <codigoPorcentaje>4</codigoPorcentaje>
          <tarifa>15.00</tarifa>
          <baseImponible>75.00</baseImponible>
          <valor>11.25</valor>
        </impuesto>
      </impuestos>
    </detalle>
  </detalles>
  <infoAdicional>
    <campoAdicional nombre=""Email"">juan.perez@test.com</campoAdicional>
    <campoAdicional nombre=""Direccion"">Guayaquil, Ecuador</campoAdicional>
  </infoAdicional>
</factura>";

        var dto = _parser.ParsearFacturaXml(xml, "AUTORIZADO");

        dto.ClaveAcceso.ShouldBe("0101202601099999999900120010010000000421234567812");
        dto.Establecimiento.ShouldBe("001");
        dto.PuntoEmision.ShouldBe("002");
        dto.Secuencial.ShouldBe("000000042");
        dto.FechaEmision.Day.ShouldBe(15);
        dto.FechaEmision.Month.ShouldBe(2);
        dto.FechaEmision.Year.ShouldBe(2026);
        dto.TotalSinImpuestos.ShouldBe(80.00m);
        dto.TotalDescuento.ShouldBe(5.00m);
        dto.ImporteTotal.ShouldBe(86.25m);
        dto.FormaPago.ShouldBe("20");

        // Comprador
        dto.Comprador.TipoIdentificacion.ShouldBe("05");
        dto.Comprador.Identificacion.ShouldBe("0923456789");
        dto.Comprador.RazonSocial.ShouldBe("JUAN PEREZ");
        dto.Comprador.CorreoElectronico.ShouldBe("juan.perez@test.com");
        dto.Comprador.Direccion.ShouldBe("Guayaquil, Ecuador");

        // Detalles
        dto.Detalles.Count.ShouldBe(1);
        dto.Detalles[0].CodigoPrincipal.ShouldBe("SERV-01");
        dto.Detalles[0].PrecioUnitario.ShouldBe(80.00m);
        dto.Detalles[0].Descuento.ShouldBe(5.00m);
        dto.Detalles[0].Impuestos.Count.ShouldBe(1);
        dto.Detalles[0].Impuestos[0].Valor.ShouldBe(11.25m);
    }

    [Test]
    public void EnriquecerEmisorDesdeFacturas_DebeExtraerMaxSecuencialYDatosTributarios()
    {
        var cliente = new ClienteMigracionDto
        {
            NombreOrganizacion = "Cliente Sin Datos",
            UltimoSecuencialFactura = 0,
            Facturas =
            [
                new FacturaMigracionDto
                {
                    Secuencial = "000000010",
                    XmlOriginal = @"<factura><infoTributaria><ruc>0912345678001</ruc><razonSocial>EMISOR TEST</razonSocial><dirMatriz>Quito</dirMatriz><estab>002</estab><ptoEmi>003</ptoEmi><ambiente>2</ambiente></infoTributaria></factura>"
                },
                new FacturaMigracionDto
                {
                    Secuencial = "000000095",
                    XmlOriginal = @"<factura><infoTributaria><ruc>0912345678001</ruc><razonSocial>EMISOR TEST</razonSocial><dirMatriz>Quito</dirMatriz><estab>002</estab><ptoEmi>003</ptoEmi><ambiente>2</ambiente></infoTributaria></factura>"
                }
            ]
        };

        _parser.EnriquecerEmisorDesdeFacturas(cliente);

        cliente.Ruc.ShouldBe("0912345678001");
        cliente.RazonSocial.ShouldBe("EMISOR TEST");
        cliente.CodigoEstablecimiento.ShouldBe("002");
        cliente.PuntoEmision.ShouldBe("003");
        cliente.Ambiente.ShouldBe(2);
        cliente.UltimoSecuencialFactura.ShouldBe(95);
    }

    [Test]
    public void ValidarCertificado_SinCertificado_DebeRetornarInvalidoSinExcepcion()
    {
        var res = _certValidator.ValidarCertificado(null, null);

        res.EsValido.ShouldBeFalse();
        res.CertificadoBytes.ShouldBeEmpty();
        res.ErrorMensaje.ShouldNotBeNull();
    }

    [Test]
    public void ValidarCertificado_Base64Invalido_DebeRetornarErrorFormato()
    {
        var res = _certValidator.ValidarCertificado("EsteNoEsUnBase64Valido!@#$", "clave123");

        res.EsValido.ShouldBeFalse();
        res.ErrorMensaje.ShouldNotBeNull();
        res.ErrorMensaje!.ShouldContain("Base64");
    }

    [Test]
    public void NormalizarTipoIdentificacion_CasosDiversos_DebeMapearCorrectamente()
    {
        // Códigos directos
        SqlLegacyDbSource.NormalizarTipoIdentificacion("04", "1790016919001").ShouldBe("04");
        SqlLegacyDbSource.NormalizarTipoIdentificacion("05", "1712345678").ShouldBe("05");
        SqlLegacyDbSource.NormalizarTipoIdentificacion("06", "A12345678").ShouldBe("06");
        SqlLegacyDbSource.NormalizarTipoIdentificacion("07", "9999999999999").ShouldBe("07");
        SqlLegacyDbSource.NormalizarTipoIdentificacion("08", "EXT-12345").ShouldBe("08");

        // Por texto descriptivo
        SqlLegacyDbSource.NormalizarTipoIdentificacion("RUC", "1790016919001").ShouldBe("04");
        SqlLegacyDbSource.NormalizarTipoIdentificacion("Cédula", "1712345678").ShouldBe("05");
        SqlLegacyDbSource.NormalizarTipoIdentificacion("CEDULA", "1712345678").ShouldBe("05");
        SqlLegacyDbSource.NormalizarTipoIdentificacion("Pasaporte", "PASS123").ShouldBe("06");
        SqlLegacyDbSource.NormalizarTipoIdentificacion("Consumidor Final", "9999999999999").ShouldBe("07");

        // Inferir por número de identificación si tipo es nulo
        SqlLegacyDbSource.NormalizarTipoIdentificacion(null, "1790016919001").ShouldBe("04");
        SqlLegacyDbSource.NormalizarTipoIdentificacion(null, "1712345678").ShouldBe("05");
        SqlLegacyDbSource.NormalizarTipoIdentificacion(null, "9999999999999").ShouldBe("07");
    }

    [Test]
    public void DeterminarIva_CasosDiversos_DebeCalcularCodigoYTarifaCorrectos()
    {
        // Por código de porcentaje
        var res4 = SqlLegacyDbSource.DeterminarIva("4", null);
        res4.CodigoPorcentaje.ShouldBe("4");
        res4.Tarifa.ShouldBe(15.00m);

        var res2 = SqlLegacyDbSource.DeterminarIva("2", null);
        res2.CodigoPorcentaje.ShouldBe("2");
        res2.Tarifa.ShouldBe(12.00m);

        var res0 = SqlLegacyDbSource.DeterminarIva("0", null);
        res0.CodigoPorcentaje.ShouldBe("0");
        res0.Tarifa.ShouldBe(0.00m);

        // Por tarifa numérica directa
        var resTarifa15 = SqlLegacyDbSource.DeterminarIva(null, 15.00m);
        resTarifa15.CodigoPorcentaje.ShouldBe("4");
        resTarifa15.Tarifa.ShouldBe(15.00m);

        var resTarifa12 = SqlLegacyDbSource.DeterminarIva(null, 12.00m);
        resTarifa12.CodigoPorcentaje.ShouldBe("2");
        resTarifa12.Tarifa.ShouldBe(12.00m);

        // Tarifa como fracción decimal (0.15 => 15%)
        var resFraccion = SqlLegacyDbSource.DeterminarIva(null, 0.15m);
        resFraccion.CodigoPorcentaje.ShouldBe("4");
        resFraccion.Tarifa.ShouldBe(15.00m);

        // Valor por defecto sin parámetros
        var resDef = SqlLegacyDbSource.DeterminarIva(null, null);
        resDef.CodigoPorcentaje.ShouldBe("4");
        resDef.Tarifa.ShouldBe(15.00m);
    }

    [Test]
    public void BuscarColumna_DiccionarioConColumnas_DebeBuscarCaseInsensitive()
    {
        var columnas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["creatorid"] = "uniqueidentifier",
            ["precio_unitario"] = "decimal",
            ["RazonSocial"] = "nvarchar"
        };

        SqlLegacyDbSource.BuscarColumna(columnas, "CreatorUserId", "CreatorId").ShouldBe("creatorid");
        SqlLegacyDbSource.BuscarColumna(columnas, "Name", "RazonSocial").ShouldBe("RazonSocial");
        SqlLegacyDbSource.BuscarColumna(columnas, "Inexistente", "Otra").ShouldBeNull();
    }

    [Test]
    public void ClienteMigracionDto_DebeSoportarColeccionesDeCompradoresYProductosDirectos()
    {
        var dto = new ClienteMigracionDto();
        dto.Compradores.ShouldNotBeNull();
        dto.Compradores.ShouldBeEmpty();
        dto.Productos.ShouldNotBeNull();
        dto.Productos.ShouldBeEmpty();

        dto.Compradores.Add(new CompradorMigracionDto
        {
            TipoIdentificacion = "04",
            Identificacion = "1790016919001",
            RazonSocial = "CLIENTE DIRECTO S.A.",
            CorreoElectronico = "cliente@directo.com"
        });

        dto.Productos.Add(new DetalleFacturaMigracionDto
        {
            CodigoPrincipal = "PROD-DIR-01",
            Descripcion = "PRODUCTO DIRECTO CATALOGO",
            PrecioUnitario = 45.50m
        });

        dto.Compradores.Count.ShouldBe(1);
        dto.Productos.Count.ShouldBe(1);
        dto.Compradores[0].RazonSocial.ShouldBe("CLIENTE DIRECTO S.A.");
        dto.Productos[0].CodigoPrincipal.ShouldBe("PROD-DIR-01");
    }

    [Test]
    public void SqlLegacyDbSource_DebeDetectarColumnasExactasDeAppClientsYAppDetails()
    {
        var appClientsCols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = "uniqueidentifier",
            ["CreatorUserId"] = "uniqueidentifier",
            ["TipoIdentificacionComprador"] = "nvarchar",
            ["RazonSocialComprador"] = "nvarchar",
            ["IdentificacionComprador"] = "nvarchar",
            ["DireccionComprador"] = "nvarchar",
            ["ContribuyenteRimpe"] = "bit",
            ["Correo"] = "nvarchar",
            ["IsDeleted"] = "bit"
        };

        SqlLegacyDbSource.BuscarColumna(appClientsCols, "CreatorUserId", "CreatorId", "UserId").ShouldBe("CreatorUserId");
        SqlLegacyDbSource.BuscarColumna(appClientsCols, "IdentificacionComprador", "Identification").ShouldBe("IdentificacionComprador");
        SqlLegacyDbSource.BuscarColumna(appClientsCols, "RazonSocialComprador", "Name").ShouldBe("RazonSocialComprador");
        SqlLegacyDbSource.BuscarColumna(appClientsCols, "TipoIdentificacionComprador", "IdentificationType").ShouldBe("TipoIdentificacionComprador");
        SqlLegacyDbSource.BuscarColumna(appClientsCols, "Correo", "Email").ShouldBe("Correo");
        SqlLegacyDbSource.BuscarColumna(appClientsCols, "DireccionComprador", "Address").ShouldBe("DireccionComprador");

        var appDetailsCols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = "uniqueidentifier",
            ["CreatorUserId"] = "uniqueidentifier",
            ["Nombre"] = "nvarchar",
            ["Precio"] = "decimal",
            ["CodigoPorcentaje"] = "tinyint",
            ["CodigoImpuesto"] = "tinyint",
            ["CodigoAuxiliar"] = "nvarchar",
            ["IsDeleted"] = "bit"
        };

        SqlLegacyDbSource.BuscarColumna(appDetailsCols, "Id").ShouldBe("Id");
        SqlLegacyDbSource.BuscarColumna(appDetailsCols, "CreatorUserId", "CreatorId", "UserId").ShouldBe("CreatorUserId");
        SqlLegacyDbSource.BuscarColumna(appDetailsCols, "CodigoAuxiliar", "Code").ShouldBe("CodigoAuxiliar");
        SqlLegacyDbSource.BuscarColumna(appDetailsCols, "Nombre", "Name").ShouldBe("Nombre");
        SqlLegacyDbSource.BuscarColumna(appDetailsCols, "Precio", "Price").ShouldBe("Precio");
        SqlLegacyDbSource.BuscarColumna(appDetailsCols, "CodigoPorcentaje", "TaxPercentageCode").ShouldBe("CodigoPorcentaje");
        SqlLegacyDbSource.BuscarColumna(appDetailsCols, "CodigoImpuesto", "TaxCode").ShouldBe("CodigoImpuesto");
    }
}
