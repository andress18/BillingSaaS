using System;
using System.Threading;
using System.Threading.Tasks;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Domain.Exceptions;
using BillingSaaS.Infrastructure.Data;
using BillingSaaS.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.UnitTests.Suscripciones;

[TestFixture]
public class SubscriptionValidationServiceTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private Mock<TimeProvider> _timeProviderMock = null!;
    private SubscriptionValidationService _service = null!;
    private readonly DateTime _now = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(_now));

        _service = new SubscriptionValidationService(_context, _timeProviderMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task ValidarEmisionAsync_SinSuscripcion_DebeLanzarSubscriptionRequiredException()
    {
        var tenantId = Guid.NewGuid();

        await Should.ThrowAsync<SubscriptionRequiredException>(() =>
            _service.ValidarEmisionAsync(tenantId, "01", "001", CancellationToken.None));
    }

    [Test]
    public async Task ValidarEmisionAsync_SuscripcionExpiradaFueraDeGracia_DebeLanzarSubscriptionExpiredException()
    {
        var tenantId = Guid.NewGuid();
        var plan = Plan.Crear("COMERCIO_PRO", "Comercio Pro", "Ilimitado", 10m, 99m, null, null, 1, "01,04", true);
        _context.Planes.Add(plan);
        await _context.SaveChangesAsync();

        // Venció hace 10 días (gracia es 5 días)
        var sub = TenantSubscription.Crear(
            tenantId,
            plan.Id,
            _now.AddMonths(-2),
            _now.AddDays(-10),
            "MENSUAL"
        );
        _context.Suscripciones.Add(sub);
        await _context.SaveChangesAsync();

        await Should.ThrowAsync<SubscriptionExpiredException>(() =>
            _service.ValidarEmisionAsync(tenantId, "01", "001", CancellationToken.None));
    }

    [Test]
    public async Task ValidarEmisionAsync_DentroDePeriodoGracia_DebePermitirEmision()
    {
        var tenantId = Guid.NewGuid();
        var plan = Plan.Crear("COMERCIO_PRO", "Comercio Pro", "Ilimitado", 10m, 99m, null, null, 1, "01,04", true);
        _context.Planes.Add(plan);
        await _context.SaveChangesAsync();

        // Venció hace 2 días (dentro de periodo de gracia de 5 días)
        var sub = TenantSubscription.Crear(
            tenantId,
            plan.Id,
            _now.AddMonths(-1),
            _now.AddDays(-2),
            "MENSUAL"
        );
        _context.Suscripciones.Add(sub);
        await _context.SaveChangesAsync();

        // No debe lanzar excepción
        await Should.NotThrowAsync(() =>
            _service.ValidarEmisionAsync(tenantId, "01", "001", CancellationToken.None));
    }

    [Test]
    public async Task ValidarEmisionAsync_TipoComprobanteNoPermitido_DebeLanzarDocumentTypeNotAllowedException()
    {
        var tenantId = Guid.NewGuid();
        // Plan que solo permite Facturas (01) y Notas de Crédito (04)
        var plan = Plan.Crear("EMPRENDEDOR", "Emprendedor", "Básico", 5m, 50m, 30, 360, 1, "01,04", true);
        _context.Planes.Add(plan);
        await _context.SaveChangesAsync();

        var sub = TenantSubscription.Crear(
            tenantId,
            plan.Id,
            _now.AddDays(-5),
            _now.AddDays(25),
            "MENSUAL"
        );
        _context.Suscripciones.Add(sub);
        await _context.SaveChangesAsync();

        // Intentar emitir Guía de Remisión (06)
        var ex = await Should.ThrowAsync<DocumentTypeNotAllowedException>(() =>
            _service.ValidarEmisionAsync(tenantId, "06", "001", CancellationToken.None));

        ex.TipoComprobante.ShouldBe("06");
        ex.PlanNombre.ShouldBe("Emprendedor");
    }

    [Test]
    public async Task ValidarEmisionAsync_LimiteEstablecimientosSuperado_DebeLanzarEstablishmentLimitExceededException()
    {
        var tenantId = Guid.NewGuid();
        // Plan solo permite 1 establecimiento
        var plan = Plan.Crear("EMPRENDEDOR", "Emprendedor", "Básico", 5m, 50m, 30, 360, 1, "01,04", true);
        _context.Planes.Add(plan);
        await _context.SaveChangesAsync();

        var sub = TenantSubscription.Crear(
            tenantId,
            plan.Id,
            _now.AddDays(-5),
            _now.AddDays(25),
            "MENSUAL"
        );
        _context.Suscripciones.Add(sub);

        // Ya existe sucursal 001
        var emisorExistente = Emisor.Crear(
            tenantId,
            "0957790108001",
            "Mi Farmacia",
            "Matriz",
            codigoEstablecimiento: "001"
        );
        _context.Emisores.Add(emisorExistente);
        await _context.SaveChangesAsync();

        // Intento de emitir desde sucursal 002 (segunda sucursal distinta)
        var ex = await Should.ThrowAsync<EstablishmentLimitExceededException>(() =>
            _service.ValidarEmisionAsync(tenantId, "01", "002", CancellationToken.None));

        ex.MaxPermitido.ShouldBe(1);
        ex.Solicitado.ShouldBe(2);
    }

    [Test]
    public async Task ValidarEmisionAsync_LimiteDocumentosAlcanzado_DebeLanzarSubscriptionLimitExceededException()
    {
        var tenantId = Guid.NewGuid();
        // Plan permite 2 documentos al mes para testing
        var plan = Plan.Crear("TEST_LIMIT", "Plan Test", "Test", 5m, 50m, 2, 24, 1, "01,04", true);
        _context.Planes.Add(plan);
        await _context.SaveChangesAsync();

        var sub = TenantSubscription.Crear(
            tenantId,
            plan.Id,
            _now.AddDays(-10),
            _now.AddDays(20),
            "MENSUAL"
        );
        _context.Suscripciones.Add(sub);

        var emisor = Emisor.Crear(tenantId, "0957790108001", "Mi Empresa", "Dir", codigoEstablecimiento: "001");
        _context.Emisores.Add(emisor);
        await _context.SaveChangesAsync();

        // Registrar 2 facturas previas en este ciclo
        var f1 = Factura.Crear(tenantId, 1, "Mi Empresa", "0957790108001", "001", "001", "000000001", "Dir", _now.Date,
            Comprador.Crear("07", "9999999999999", "CF", "Dir"), [], emisor.Id);
        f1.MarcarComoAutorizada("1234567890123456789012345678901234567890123456789", _now);

        var f2 = Factura.Crear(tenantId, 1, "Mi Empresa", "0957790108001", "001", "001", "000000002", "Dir", _now.Date,
            Comprador.Crear("07", "9999999999999", "CF", "Dir"), [], emisor.Id);
        f2.MarcarComoAutorizada("1234567890123456789012345678901234567890123456788", _now);

        _context.Facturas.AddRange(f1, f2);
        await _context.SaveChangesAsync();

        // Intentar emitir la 3ra factura (excede límite de 2)
        var ex = await Should.ThrowAsync<SubscriptionLimitExceededException>(() =>
            _service.ValidarEmisionAsync(tenantId, "01", "001", CancellationToken.None));

        ex.LimiteDocumentos.ShouldBe(2);
        ex.Emitidos.ShouldBe(2);
    }

    [Test]
    public async Task ValidarEmisionAsync_PlanIlimitado_DebePermitirEmisionSinLimite()
    {
        var tenantId = Guid.NewGuid();
        var plan = Plan.Crear("COMERCIO_PRO", "Comercio Pro", "Ilimitado", 10m, 99m, null, null, 1, "01,04,05", true);
        _context.Planes.Add(plan);
        await _context.SaveChangesAsync();

        var sub = TenantSubscription.Crear(
            tenantId,
            plan.Id,
            _now.AddDays(-10),
            _now.AddDays(20),
            "MENSUAL"
        );
        _context.Suscripciones.Add(sub);

        var emisor = Emisor.Crear(tenantId, "0957790108001", "Mi Empresa", "Dir", codigoEstablecimiento: "001");
        _context.Emisores.Add(emisor);
        await _context.SaveChangesAsync();

        // Registrar muchas facturas emitidas
        for (int i = 1; i <= 50; i++)
        {
            var f = Factura.Crear(tenantId, 1, "Mi Empresa", "0957790108001", "001", "001", $"{i:D9}", "Dir", _now.Date,
                Comprador.Crear("07", "9999999999999", "CF", "Dir"), [], emisor.Id);
            f.MarcarComoAutorizada($"1234567890123456789012345678901234567890123456{i:D3}", _now);
            _context.Facturas.Add(f);
        }
        await _context.SaveChangesAsync();

        // En plan ilimitado no debe lanzar excepción
        await Should.NotThrowAsync(() =>
            _service.ValidarEmisionAsync(tenantId, "01", "001", CancellationToken.None));
    }

    [Test]
    public async Task ValidarEmisionAsync_LimiteAlcanzadoEnFacturas_DebePermitirNotasCreditoSiNoHaLlegadoAlLimite()
    {
        var tenantId = Guid.NewGuid();
        // Plan permite 2 documentos por mes para cada tipo
        var plan = Plan.Crear("TEST_LIMIT_SEP", "Plan Test", "Test", 5m, 50m, 2, 24, 1, "01,04,05", true);
        _context.Planes.Add(plan);
        await _context.SaveChangesAsync();

        var sub = TenantSubscription.Crear(
            tenantId,
            plan.Id,
            _now.AddDays(-10),
            _now.AddDays(20),
            "MENSUAL"
        );
        _context.Suscripciones.Add(sub);

        var emisor = Emisor.Crear(tenantId, "0957790108001", "Mi Empresa", "Dir", codigoEstablecimiento: "001");
        _context.Emisores.Add(emisor);
        await _context.SaveChangesAsync();

        // 2 facturas previas
        var f1 = Factura.Crear(tenantId, 1, "Mi Empresa", "0957790108001", "001", "001", "000000001", "Dir", _now.Date,
            Comprador.Crear("07", "9999999999999", "CF", "Dir"), [], emisor.Id);
        f1.MarcarComoAutorizada("1234567890123456789012345678901234567890123456789", _now);

        var f2 = Factura.Crear(tenantId, 1, "Mi Empresa", "0957790108001", "001", "001", "000000002", "Dir", _now.Date,
            Comprador.Crear("07", "9999999999999", "CF", "Dir"), [], emisor.Id);
        f2.MarcarComoAutorizada("1234567890123456789012345678901234567890123456788", _now);

        _context.Facturas.AddRange(f1, f2);
        await _context.SaveChangesAsync();

        // Factura (01) debe bloquearse
        await Should.ThrowAsync<SubscriptionLimitExceededException>(() =>
            _service.ValidarEmisionAsync(tenantId, "01", "001", CancellationToken.None));

        // Nota de Crédito (04) debe permitirse porque no tiene notas de crédito emitidas en este ciclo
        await Should.NotThrowAsync(() =>
            _service.ValidarEmisionAsync(tenantId, "04", "001", CancellationToken.None));

        // Nota de Débito (05) debe permitirse
        await Should.NotThrowAsync(() =>
            _service.ValidarEmisionAsync(tenantId, "05", "001", CancellationToken.None));
    }
}

