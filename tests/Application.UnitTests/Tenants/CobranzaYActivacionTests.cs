using System;
using System.Threading;
using System.Threading.Tasks;
using BillingSaaS.Application.Common.Exceptions;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Application.Tenants.Commands.ActivarTenant;
using BillingSaaS.Domain.Constants;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Domain.Exceptions;
using BillingSaaS.Infrastructure.Data;
using BillingSaaS.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.UnitTests.Tenants;

[TestFixture]
public class CobranzaYActivacionTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private Mock<IUser> _userMock = null!;
    private Mock<IIdentityService> _identityServiceMock = null!;
    private Mock<TimeProvider> _timeProviderMock = null!;

    private readonly DateTime _now = new(2026, 10, 4, 10, 0, 0, DateTimeKind.Utc);
    private readonly string _adminUserId = "admin-user-001";
    private readonly string _partnerUserId = "partner-user-002";

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

        _userMock = new Mock<IUser>();
        _identityServiceMock = new Mock<IIdentityService>();
        _timeProviderMock = new Mock<TimeProvider>();

        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(_now));
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task ValidarEmision_TenantEnPendientePago_DebeLanzarTenantPaymentRequiredException()
    {
        // Arrange
        var tenant = Tenant.Crear("Cliente Nuevo", null, estado: TenantEstados.PendientePago);
        _context.Tenants.Add(tenant);

        var plan = Plan.Crear("MIGRACION_SISTEMA", "Plan Migración", "Desc", 3.00m, 20.00m, null, 100, 1, "01,04");
        _context.Planes.Add(plan);
        await _context.SaveChangesAsync();

        var sub = TenantSubscription.Crear(tenant.Id, plan.Id, _now, _now.AddYears(1), "ANUAL", estado: TenantEstados.PendientePago);
        _context.Suscripciones.Add(sub);

        var emisor = Emisor.Crear(tenant.Id, "1790000000001", "Razon", "Matriz", "001", "001", 1, false);
        _context.Emisores.Add(emisor);
        await _context.SaveChangesAsync();

        var validator = new SubscriptionValidationService(_context, _timeProviderMock.Object);

        // Act & Assert
        var ex = await Should.ThrowAsync<TenantPaymentRequiredException>(() =>
            validator.ValidarEmisionAsync(tenant.Id, "01", "001", CancellationToken.None));

        ex.TenantId.ShouldBe(tenant.Id);
        ex.NombreOrganizacion.ShouldBe("Cliente Nuevo");
    }

    [Test]
    public async Task ValidarEmision_SuscripcionEnPendientePago_DebeLanzarTenantPaymentRequiredException()
    {
        // Arrange
        var tenant = Tenant.Crear("Cliente Con Sub Pendiente", null, estado: TenantEstados.Activo);
        _context.Tenants.Add(tenant);

        var plan = Plan.Crear("MIGRACION_SISTEMA", "Plan Migración", "Desc", 3.00m, 20.00m, null, 100, 1, "01,04");
        _context.Planes.Add(plan);
        await _context.SaveChangesAsync();

        var sub = TenantSubscription.Crear(tenant.Id, plan.Id, _now, _now.AddYears(1), "ANUAL", estado: TenantEstados.PendientePago);
        _context.Suscripciones.Add(sub);

        var emisor = Emisor.Crear(tenant.Id, "1790000000001", "Razon", "Matriz", "001", "001", 1, false);
        _context.Emisores.Add(emisor);
        await _context.SaveChangesAsync();

        var validator = new SubscriptionValidationService(_context, _timeProviderMock.Object);

        // Act & Assert
        var ex = await Should.ThrowAsync<TenantPaymentRequiredException>(() =>
            validator.ValidarEmisionAsync(tenant.Id, "01", "001", CancellationToken.None));

        ex.TenantId.ShouldBe(tenant.Id);
    }

    [Test]
    public async Task ValidarEmision_TenantYSuscripcionActivos_DebePermitirEmision()
    {
        // Arrange
        var tenant = Tenant.Crear("Cliente Activo", null, estado: TenantEstados.Activo);
        _context.Tenants.Add(tenant);

        var plan = Plan.Crear("MIGRACION_SISTEMA", "Plan Migración", "Desc", 3.00m, 20.00m, null, 100, 1, "01,04");
        _context.Planes.Add(plan);
        await _context.SaveChangesAsync();

        var sub = TenantSubscription.Crear(tenant.Id, plan.Id, _now, _now.AddYears(1), "ANUAL", estado: TenantEstados.Activo);
        _context.Suscripciones.Add(sub);

        var emisor = Emisor.Crear(tenant.Id, "1790000000001", "Razon", "Matriz", "001", "001", 1, false);
        _context.Emisores.Add(emisor);
        await _context.SaveChangesAsync();

        var validator = new SubscriptionValidationService(_context, _timeProviderMock.Object);

        // Act & Assert (no lanza excepción)
        await validator.ValidarEmisionAsync(tenant.Id, "01", "001", CancellationToken.None);
    }

    [Test]
    public async Task ActivarTenant_ComoAdminPorTenantId_DebeCambiarEstadoAActivoYVencimientoA365Dias()
    {
        // Arrange
        _userMock.Setup(u => u.Id).Returns(_adminUserId);
        _identityServiceMock.Setup(i => i.IsInRoleAsync(_adminUserId, Roles.Administrator)).ReturnsAsync(true);

        var tenant = Tenant.Crear("Comercial Los Andes", null, estado: TenantEstados.PendientePago);
        _context.Tenants.Add(tenant);

        var plan = Plan.Crear("MIGRACION_SISTEMA", "Plan Migración", "Desc", 3.00m, 20.00m, null, 100, 1, "01,04");
        _context.Planes.Add(plan);
        await _context.SaveChangesAsync();

        var sub = TenantSubscription.Crear(tenant.Id, plan.Id, _now.AddDays(-10), _now.AddDays(30), "ANUAL", estado: TenantEstados.PendientePago);
        _context.Suscripciones.Add(sub);
        await _context.SaveChangesAsync();

        var handler = new ActivarTenantCommandHandler(
            _context,
            _identityServiceMock.Object,
            _userMock.Object,
            _timeProviderMock.Object);

        // Act
        var result = await handler.Handle(new ActivarTenantCommand
        {
            TenantId = tenant.Id,
            DiasVigencia = 365
        }, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.TenantId.ShouldBe(tenant.Id);
        result.Estado.ShouldBe(TenantEstados.Activo);
        result.FechaActivacion.ShouldBe(_now);
        result.FechaVencimiento.ShouldBe(_now.AddDays(365));

        var tenantDb = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.Id);
        tenantDb.ShouldNotBeNull();
        tenantDb.Estado.ShouldBe(TenantEstados.Activo);
        tenantDb.Activo.ShouldBeTrue();
        tenantDb.FechaActivacion.ShouldBe(_now);
        tenantDb.TokenActivacion.ShouldBeNull();

        var subDb = await _context.Suscripciones.FirstOrDefaultAsync(s => s.TenantId == tenant.Id);
        subDb.ShouldNotBeNull();
        subDb.Estado.ShouldBe(TenantEstados.Activo);
        subDb.FechaInicio.ShouldBe(_now);
        subDb.FechaVencimiento.ShouldBe(_now.AddDays(365));
    }

    [Test]
    public async Task ActivarTenant_ComoAdminPorToken_DebeActivarExitosamente()
    {
        // Arrange
        _userMock.Setup(u => u.Id).Returns(_adminUserId);
        _identityServiceMock.Setup(i => i.IsInRoleAsync(_adminUserId, Roles.Administrator)).ReturnsAsync(true);

        var tenant = Tenant.Crear("Distribuidora Norte", null, estado: TenantEstados.PendientePago);
        _context.Tenants.Add(tenant);

        var plan = Plan.Crear("MIGRACION_SISTEMA", "Plan Migración", "Desc", 3.00m, 20.00m, null, 100, 1, "01,04");
        _context.Planes.Add(plan);
        await _context.SaveChangesAsync();

        var sub = TenantSubscription.Crear(tenant.Id, plan.Id, _now, _now.AddYears(1), "ANUAL", estado: TenantEstados.PendientePago);
        _context.Suscripciones.Add(sub);
        await _context.SaveChangesAsync();

        var token = tenant.TokenActivacion;
        token.ShouldNotBeNullOrWhiteSpace();

        var handler = new ActivarTenantCommandHandler(
            _context,
            _identityServiceMock.Object,
            _userMock.Object,
            _timeProviderMock.Object);

        // Act
        var result = await handler.Handle(new ActivarTenantCommand
        {
            TokenActivacion = token,
            DiasVigencia = 365
        }, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.TenantId.ShouldBe(tenant.Id);
        result.Estado.ShouldBe(TenantEstados.Activo);

        var tenantDb = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.Id);
        tenantDb!.Estado.ShouldBe(TenantEstados.Activo);
        tenantDb.TokenActivacion.ShouldBeNull();
    }

    [Test]
    public async Task ActivarTenant_ComoPartner_DebeLanzarForbiddenAccessException()
    {
        // Arrange: Partner intenta activar sin transferir comisiones
        _userMock.Setup(u => u.Id).Returns(_partnerUserId);
        _identityServiceMock.Setup(i => i.IsInRoleAsync(_partnerUserId, Roles.Administrator)).ReturnsAsync(false);
        _identityServiceMock.Setup(i => i.IsInRoleAsync(_partnerUserId, Roles.Partner)).ReturnsAsync(true);

        var tenant = Tenant.Crear("Cliente Partner", null, estado: TenantEstados.PendientePago);
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        var handler = new ActivarTenantCommandHandler(
            _context,
            _identityServiceMock.Object,
            _userMock.Object,
            _timeProviderMock.Object);

        // Act & Assert
        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            handler.Handle(new ActivarTenantCommand { TenantId = tenant.Id }, CancellationToken.None));
    }
}
