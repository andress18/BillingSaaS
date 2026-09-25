using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillingSaaS.Application.Common.Exceptions;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Application.Common.Models;
using BillingSaaS.Application.Tenants.Commands.OnboardingCliente;
using BillingSaaS.Application.Tenants.Queries.GetClientesCartera;
using BillingSaaS.Domain.Constants;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.UnitTests.Tenants;

[TestFixture]
public class OnboardingClienteTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private Mock<IUser> _userMock = null!;
    private Mock<IIdentityService> _identityServiceMock = null!;
    private Mock<ICertificateEncryptionService> _encryptionServiceMock = null!;
    private Mock<TimeProvider> _timeProviderMock = null!;

    private readonly DateTime _now = new(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc);
    private readonly Guid _partnerTenantId = Guid.NewGuid();
    private readonly string _partnerUserId = "partner-user-123";
    private readonly string _adminUserId = "admin-user-999";

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
        _encryptionServiceMock = new Mock<ICertificateEncryptionService>();
        _timeProviderMock = new Mock<TimeProvider>();

        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(_now));

        // Default mock user creation succeeds
        _identityServiceMock
            .Setup(i => i.CreateUserWithTenantAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync((Result.Success(), Guid.NewGuid().ToString()));

        _identityServiceMock
            .Setup(i => i.CreateUserWithTenantAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string?>()))
            .ReturnsAsync((Result.Success(), Guid.NewGuid().ToString()));

        // Seed migration plans
        _context.Planes.AddRange(
            Plan.Crear("MIGRACION_SISTEMA", "Plan Migración (Solo Sistema)", "Solo sistema", 3m, 20m, null, null, 1, "01,04"),
            Plan.Crear("MIGRACION_FIRMA", "Plan Migración + Firma Digital", "Con firma", 5m, 45m, null, null, 1, "01,04")
        );
        _context.SaveChanges();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task OnboardingCliente_ComoPartner_DebeCrearTenantConPartnerIdYSuscripcionActiva()
    {
        // Arrange
        _userMock.Setup(u => u.Id).Returns(_partnerUserId);
        _userMock.Setup(u => u.TenantId).Returns(_partnerTenantId);

        _identityServiceMock.Setup(i => i.IsInRoleAsync(_partnerUserId, Roles.Partner)).ReturnsAsync(true);
        _identityServiceMock.Setup(i => i.IsInRoleAsync(_partnerUserId, Roles.Administrator)).ReturnsAsync(false);

        var handler = new OnboardingClienteCommandHandler(
            _context,
            _identityServiceMock.Object,
            _userMock.Object,
            _encryptionServiceMock.Object,
            _timeProviderMock.Object);

        var command = new OnboardingClienteCommand
        {
            NombreOrganizacion = "Ferretería San José",
            Email = "sanjose@gmail.com",
            PlanCodigo = "MIGRACION_SISTEMA",
            Frecuencia = "ANUAL"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.NombreOrganizacion.ShouldBe("Ferretería San José");
        result.Email.ShouldBe("sanjose@gmail.com");
        result.PartnerId.ShouldBe(_partnerTenantId);
        result.PlanCodigo.ShouldBe("MIGRACION_SISTEMA");
        result.FechaVencimiento.ShouldBe(_now.AddYears(1));

        var tenantDb = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == result.TenantId);
        tenantDb.ShouldNotBeNull();
        tenantDb.PartnerId.ShouldBe(_partnerTenantId);

        var subDb = await _context.Suscripciones.FirstOrDefaultAsync(s => s.TenantId == result.TenantId);
        subDb.ShouldNotBeNull();
        subDb.Estado.ShouldBe("ACTIVO");
        subDb.FechaVencimiento.ShouldBe(_now.AddYears(1));
    }

    [Test]
    public async Task OnboardingCliente_ComoAdminConPartnerId_DebeAsignarPartnerIdIndicado()
    {
        // Arrange
        _userMock.Setup(u => u.Id).Returns(_adminUserId);
        _identityServiceMock.Setup(i => i.IsInRoleAsync(_adminUserId, Roles.Administrator)).ReturnsAsync(true);
        _identityServiceMock.Setup(i => i.IsInRoleAsync(_adminUserId, Roles.Partner)).ReturnsAsync(false);

        var customPartnerId = Guid.NewGuid();

        var handler = new OnboardingClienteCommandHandler(
            _context,
            _identityServiceMock.Object,
            _userMock.Object,
            _encryptionServiceMock.Object,
            _timeProviderMock.Object);

        var command = new OnboardingClienteCommand
        {
            NombreOrganizacion = "Farmacia Central",
            Email = "central@gmail.com",
            PlanCodigo = "MIGRACION_FIRMA",
            Frecuencia = "ANUAL",
            PartnerId = customPartnerId
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.PartnerId.ShouldBe(customPartnerId);
        result.PlanCodigo.ShouldBe("MIGRACION_FIRMA");

        var tenantDb = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == result.TenantId);
        tenantDb.ShouldNotBeNull();
        tenantDb.PartnerId.ShouldBe(customPartnerId);
    }

    [Test]
    public async Task OnboardingCliente_UsuarioSinRolAdecuado_DebeLanzarForbiddenAccessException()
    {
        _userMock.Setup(u => u.Id).Returns("user-normal");
        _identityServiceMock.Setup(i => i.IsInRoleAsync("user-normal", It.IsAny<string>())).ReturnsAsync(false);

        var handler = new OnboardingClienteCommandHandler(
            _context,
            _identityServiceMock.Object,
            _userMock.Object,
            _encryptionServiceMock.Object,
            _timeProviderMock.Object);

        var command = new OnboardingClienteCommand
        {
            NombreOrganizacion = "Test Invalido",
            Email = "test@gmail.com"
        };

        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Test]
    public async Task GetClientesCartera_ComoPartner_SoloDebeRetornarClientesDeSuCartera()
    {
        // Arrange
        var partner1TenantId = Guid.NewGuid();
        var partner2TenantId = Guid.NewGuid();

        var tenant1 = Tenant.Crear("Cliente Partner 1", partner1TenantId);
        var tenant2 = Tenant.Crear("Cliente Partner 2", partner2TenantId);
        var tenantDirecto = Tenant.Crear("Cliente Directo", null);

        _context.Tenants.AddRange(tenant1, tenant2, tenantDirecto);
        await _context.SaveChangesAsync();

        _userMock.Setup(u => u.Id).Returns(_partnerUserId);
        _userMock.Setup(u => u.TenantId).Returns(partner1TenantId);

        _identityServiceMock.Setup(i => i.IsInRoleAsync(_partnerUserId, Roles.Partner)).ReturnsAsync(true);
        _identityServiceMock.Setup(i => i.IsInRoleAsync(_partnerUserId, Roles.Administrator)).ReturnsAsync(false);

        var handler = new GetClientesCarteraQueryHandler(
            _context,
            _identityServiceMock.Object,
            _userMock.Object,
            _timeProviderMock.Object);

        // Act
        var result = await handler.Handle(new GetClientesCarteraQuery(), CancellationToken.None);

        // Assert
        result.Count.ShouldBe(1);
        result.First().NombreOrganizacion.ShouldBe("Cliente Partner 1");
        result.First().PartnerId.ShouldBe(partner1TenantId);
    }

    [Test]
    public async Task GetClientesCartera_ComoAdmin_DebeRetornarTodosLosClientes()
    {
        // Arrange
        var partnerId = Guid.NewGuid();
        var tenant1 = Tenant.Crear("Cliente P", partnerId);
        var tenant2 = Tenant.Crear("Cliente Directo", null);

        _context.Tenants.AddRange(tenant1, tenant2);
        await _context.SaveChangesAsync();

        _userMock.Setup(u => u.Id).Returns(_adminUserId);
        _identityServiceMock.Setup(i => i.IsInRoleAsync(_adminUserId, Roles.Administrator)).ReturnsAsync(true);
        _identityServiceMock.Setup(i => i.IsInRoleAsync(_adminUserId, Roles.Partner)).ReturnsAsync(false);

        var handler = new GetClientesCarteraQueryHandler(
            _context,
            _identityServiceMock.Object,
            _userMock.Object,
            _timeProviderMock.Object);

        // Act
        var result = await handler.Handle(new GetClientesCarteraQuery(), CancellationToken.None);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContain(c => c.NombreOrganizacion == "Cliente P");
        result.ShouldContain(c => c.NombreOrganizacion == "Cliente Directo");
    }

    [Test]
    public async Task OnboardingCliente_ConSoloUsername_DebeCrearUsuarioConUsernameYEmailNull()
    {
        // Arrange
        _userMock.Setup(u => u.Id).Returns(_partnerUserId);
        _userMock.Setup(u => u.TenantId).Returns(_partnerTenantId);
        _identityServiceMock.Setup(i => i.IsInRoleAsync(_partnerUserId, Roles.Partner)).ReturnsAsync(true);
        _identityServiceMock.Setup(i => i.IsInRoleAsync(_partnerUserId, Roles.Administrator)).ReturnsAsync(false);

        string? capturedUsername = null;
        string? capturedEmail = null;
        _identityServiceMock
            .Setup(i => i.CreateUserWithTenantAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string?>()))
            .Callback<string, string?, string, Guid, string?>((u, e, p, t, r) =>
            {
                capturedUsername = u;
                capturedEmail = e;
            })
            .ReturnsAsync((Result.Success(), Guid.NewGuid().ToString()));

        var handler = new OnboardingClienteCommandHandler(
            _context,
            _identityServiceMock.Object,
            _userMock.Object,
            _encryptionServiceMock.Object,
            _timeProviderMock.Object);

        var command = new OnboardingClienteCommand
        {
            NombreOrganizacion = "Distribuidora Los Andes",
            Username = "losandes_ec",
            Email = null,
            PlanCodigo = "MIGRACION_SISTEMA",
            Frecuencia = "ANUAL"
        };

        // Act
        var response = await handler.Handle(command, CancellationToken.None);

        // Assert
        response.ShouldNotBeNull();
        response.Username.ShouldBe("losandes_ec");
        response.Email.ShouldBeNull();
        capturedUsername.ShouldBe("losandes_ec");
        capturedEmail.ShouldBeNull();
    }

    [Test]
    public async Task OnboardingCliente_ConEmailYUsername_DebeCrearUsuarioConAmbos()
    {
        // Arrange
        _userMock.Setup(u => u.Id).Returns(_partnerUserId);
        _userMock.Setup(u => u.TenantId).Returns(_partnerTenantId);
        _identityServiceMock.Setup(i => i.IsInRoleAsync(_partnerUserId, Roles.Partner)).ReturnsAsync(true);
        _identityServiceMock.Setup(i => i.IsInRoleAsync(_partnerUserId, Roles.Administrator)).ReturnsAsync(false);

        string? capturedUsername = null;
        string? capturedEmail = null;
        _identityServiceMock
            .Setup(i => i.CreateUserWithTenantAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string?>()))
            .Callback<string, string?, string, Guid, string?>((u, e, p, t, r) =>
            {
                capturedUsername = u;
                capturedEmail = e;
            })
            .ReturnsAsync((Result.Success(), Guid.NewGuid().ToString()));

        var handler = new OnboardingClienteCommandHandler(
            _context,
            _identityServiceMock.Object,
            _userMock.Object,
            _encryptionServiceMock.Object,
            _timeProviderMock.Object);

        var command = new OnboardingClienteCommand
        {
            NombreOrganizacion = "Comercial Pichincha",
            Username = "pichincha_corp",
            Email = "contacto@pichincha.com",
            PlanCodigo = "MIGRACION_SISTEMA",
            Frecuencia = "ANUAL"
        };

        // Act
        var response = await handler.Handle(command, CancellationToken.None);

        // Assert
        response.ShouldNotBeNull();
        response.Username.ShouldBe("pichincha_corp");
        response.Email.ShouldBe("contacto@pichincha.com");
        capturedUsername.ShouldBe("pichincha_corp");
        capturedEmail.ShouldBe("contacto@pichincha.com");
    }

    [Test]
    public void OnboardingClienteValidator_SinEmailNiUsername_DebeFallarValidacion()
    {
        var validator = new OnboardingClienteCommandValidator();
        var command = new OnboardingClienteCommand
        {
            NombreOrganizacion = "Comercial Pichincha",
            Username = null,
            Email = null
        };

        var result = validator.Validate(command);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains("correo electrónico o un nombre de usuario"));
    }
}

