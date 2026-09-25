using System;
using System.Collections.Generic;
using System.Text;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Infrastructure.Services;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.UnitTests.Facturas.Queries;

[TestFixture]
public class RidePdfGeneratorTests
{
    private RidePdfGenerator _pdfGenerator = null!;

    [SetUp]
    public void SetUp()
    {
        _pdfGenerator = new RidePdfGenerator();
    }

    [Test]
    public void GenerarFacturaRide_DeberiaGenerarPdfValido_ConCabeceraPdfYLongitudMayorACero()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var emisor = Emisor.Crear(
            tenantId: tenantId,
            ruc: "0957790108001",
            razonSocial: "EMPRESA DE PRUEBA S.A.",
            direccionMatriz: "Av. Amazonas y Naciones Unidas",
            codigoEstablecimiento: "001",
            puntoEmision: "002",
            ambiente: 1,
            obligadoContabilidad: true,
            nombreComercial: "COMERCIAL PRUEBA",
            direccionEstablecimiento: "Sucursal Norte",
            regimenRimpe: "CONTRIBUYENTE RÉGIMEN RIMPE",
            contribuyenteEspecial: "5368"
        );

        var cliente = Comprador.Crear(
            tipoIdentificacion: "04",
            identificacion: "1792060346001",
            razonSocial: "CLIENTE DE PRUEBAS CIA. LTDA.",
            direccion: "Calle Los Pinos 123",
            correoElectronico: "facturacion@clienteprueba.com"
        );

        var detalles = new List<DetalleFactura>
        {
            DetalleFactura.Crear(
                codigoPrincipal: "PROD-001",
                descripcion: "Consultoría de Software Especializada",
                cantidad: 2.00m,
                precioUnitario: 50.00m,
                descuento: 0.00m,
                impuestos: new List<Impuesto>
                {
                    Impuesto.Crear(codigo: "2", codigoPorcentaje: "4", tarifa: 15.00m, baseImponible: 100.00m)
                }
            ),
            DetalleFactura.Crear(
                codigoPrincipal: "PROD-002",
                descripcion: "Manual de Usuario Impreso",
                cantidad: 1.00m,
                precioUnitario: 20.00m,
                descuento: 0.00m,
                impuestos: new List<Impuesto>
                {
                    Impuesto.Crear(codigo: "2", codigoPorcentaje: "0", tarifa: 0.00m, baseImponible: 20.00m)
                }
            )
        };

        var factura = Factura.Crear(
            tenantId: tenantId,
            ambiente: 1,
            razonSocial: emisor.RazonSocial,
            rucEmisor: emisor.Ruc,
            establecimiento: "001",
            puntoEmision: "002",
            secuencial: "000000123",
            direccionMatriz: emisor.DireccionMatriz,
            fechaEmision: DateTime.UtcNow,
            cliente: cliente,
            detalles: detalles,
            emisorId: emisor.Id
        );

        // Clave de acceso estándar SRI de 49 dígitos
        string claveAcceso = "1609202601095779010800110010020000001231234567813";
        factura.AsignarClaveAcceso(claveAcceso);
        factura.MarcarComoAutorizada(claveAcceso, DateTime.UtcNow);

        // Act
        var pdfBytes = _pdfGenerator.GenerarFacturaRide(factura, emisor);

        // Assert
        pdfBytes.ShouldNotBeNull();
        pdfBytes.Length.ShouldBeGreaterThan(1000);

        // Validar "Magic Bytes" de la especificación PDF (%PDF)
        string magicBytes = Encoding.ASCII.GetString(pdfBytes, 0, 4);
        magicBytes.ShouldBe("%PDF");
    }

    [Test]
    public void GenerarFacturaRide_ConLogoValido_DeberiaGenerarPdfCorrectamente()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var cliente = Comprador.Crear("05", "0921234567", "JUAN PEREZ", "Guayaquil", "juan@test.com");
        var detalles = new List<DetalleFactura>
        {
            DetalleFactura.Crear(
                "ITM-01", "Servicio Técnico", 1.00m, 10.00m, 0.00m,
                new List<Impuesto> { Impuesto.Crear("2", "4", 15.00m, 10.00m) })
        };

        var factura = Factura.Crear(
            tenantId: tenantId,
            ambiente: 1,
            razonSocial: "EMISOR SIMPLE",
            rucEmisor: "0957790108001",
            establecimiento: "001",
            puntoEmision: "001",
            secuencial: "000000001",
            direccionMatriz: "Guayaquil",
            fechaEmision: DateTime.UtcNow,
            cliente: cliente,
            detalles: detalles
        );
        factura.AsignarClaveAcceso("1609202601095779010800110010010000000011234567813");

        // 1x1 transparente PNG base64
        string logoBase64 = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";

        // Act
        var pdfBytes = _pdfGenerator.GenerarFacturaRide(factura, logoBase64);

        // Assert
        pdfBytes.ShouldNotBeNull();
        pdfBytes.Length.ShouldBeGreaterThan(500);
        string magicBytes = Encoding.ASCII.GetString(pdfBytes, 0, 4);
        magicBytes.ShouldBe("%PDF");
    }

    [Test]
    public void GenerarFacturaRide_SinEmisor_UsaDatosDirectosDeFactura()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var cliente = Comprador.Crear("07", "9999999999999", "CONSUMIDOR FINAL");
        var detalles = new List<DetalleFactura>
        {
            DetalleFactura.Crear(
                "SERV-01", "Almuerzo Ejecutivo", 1.00m, 5.00m, 0.00m,
                new List<Impuesto> { Impuesto.Crear("2", "0", 0.00m, 5.00m) })
        };

        var factura = Factura.Crear(
            tenantId, 1, "RESTAURANTE SA", "0957790108001",
            "001", "001", "000000099", "Quito",
            DateTime.UtcNow, cliente, detalles);

        factura.AsignarClaveAcceso("1609202601095779010800110010010000000991234567813");

        // Act
        var pdfBytes = _pdfGenerator.GenerarFacturaRide(factura);

        // Assert
        pdfBytes.ShouldNotBeNull();
        pdfBytes.Length.ShouldBeGreaterThan(500);
        string magicBytes = Encoding.ASCII.GetString(pdfBytes, 0, 4);
        magicBytes.ShouldBe("%PDF");
    }

    [Test]
    public void GenerarFacturaRide_ConMultiplesTarifasIva_DeberiaGenerarPdfCorrectamente()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var cliente = Comprador.Crear("04", "0957790108001", "CLIENTE EMPRESARIAL", "Guayaquil", "test@test.com");
        var detalles = new List<DetalleFactura>
        {
            // Producto con IVA 15%
            DetalleFactura.Crear("P-15", "Material de Construccion 1", 10.00m, 10.00m, 0.00m,
                new List<Impuesto> { Impuesto.Crear("2", "4", 15.00m, 100.00m) }),
            // Producto con IVA 5%
            DetalleFactura.Crear("P-5", "Material de Construccion 2", 5.00m, 10.00m, 0.00m,
                new List<Impuesto> { Impuesto.Crear("2", "5", 5.00m, 50.00m) }),
            // Producto con IVA 0%
            DetalleFactura.Crear("P-0", "Material Exento", 2.00m, 10.00m, 0.00m,
                new List<Impuesto> { Impuesto.Crear("2", "0", 0.00m, 20.00m) })
        };

        var factura = Factura.Crear(
            tenantId, 1, "FERRETERIA SA", "0957790108001",
            "001", "001", "000000500", "Guayaquil",
            DateTime.UtcNow, cliente, detalles);

        factura.AsignarClaveAcceso("1609202601095779010800110010010000005001234567813");

        // Act
        var pdfBytes = _pdfGenerator.GenerarFacturaRide(factura);

        // Assert
        pdfBytes.ShouldNotBeNull();
        pdfBytes.Length.ShouldBeGreaterThan(1000);
        string magicBytes = Encoding.ASCII.GetString(pdfBytes, 0, 4);
        magicBytes.ShouldBe("%PDF");
    }

    [Test]
    public void GenerarFacturaRide_FacturaNula_LanzaArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _pdfGenerator.GenerarFacturaRide(null!));
    }

    [Test]
    public void GenerarFacturaRide_ConRimpeNegocioPopular_GeneraPdfConLeyendaOficial()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var cliente = Comprador.Crear("07", "9999999999999", "CONSUMIDOR FINAL");
        var detalles = new List<DetalleFactura>
        {
            DetalleFactura.Crear("NP-01", "Venta Artesanal", 1.00m, 15.00m, 0.00m,
                new List<Impuesto> { Impuesto.Crear("2", "0", 0.00m, 15.00m) })
        };

        var factura = Factura.Crear(
            tenantId, 1, "TALLER ARTESANAL", "0957790108001",
            "001", "001", "000000888", "Otavalo",
            DateTime.UtcNow, cliente, detalles,
            contribuyenteRimpe: "CONTRIBUYENTE NEGOCIO POPULAR - RÉGIMEN RIMPE");
        factura.AsignarClaveAcceso("1609202601095779010800110010010000008881234567813");

        // Act
        var pdfBytes = _pdfGenerator.GenerarFacturaRide(factura);

        // Assert
        pdfBytes.ShouldNotBeNull();
        pdfBytes.Length.ShouldBeGreaterThan(500);
        string magicBytes = Encoding.ASCII.GetString(pdfBytes, 0, 4);
        magicBytes.ShouldBe("%PDF");
    }

    [Test]
    public void GenerarFacturaRide_ConConfiguracionProveedorPersonalizada_GeneraPdfCorrectamente()
    {
        // Arrange
        var generator = new RidePdfGenerator(rucProveedor: "1790012345001", nombreProveedor: "Mi Sistema Pro");
        var tenantId = Guid.NewGuid();
        var cliente = Comprador.Crear("07", "9999999999999", "CONSUMIDOR FINAL");
        var detalles = new List<DetalleFactura>
        {
            DetalleFactura.Crear("SRV-01", "Servicio Custom", 1.00m, 25.00m, 0.00m,
                new List<Impuesto> { Impuesto.Crear("2", "4", 15.00m, 25.00m) })
        };

        var factura = Factura.Crear(
            tenantId, 1, "EMPRESA PRUEBA", "0957790108001",
            "001", "001", "000000777", "Quito",
            DateTime.UtcNow, cliente, detalles);
        factura.AsignarClaveAcceso("1609202601095779010800110010010000007771234567813");

        // Act
        var pdfBytes = generator.GenerarFacturaRide(factura);

        // Assert
        pdfBytes.ShouldNotBeNull();
        pdfBytes.Length.ShouldBeGreaterThan(500);
        string magicBytes = Encoding.ASCII.GetString(pdfBytes, 0, 4);
        magicBytes.ShouldBe("%PDF");
    }
}
