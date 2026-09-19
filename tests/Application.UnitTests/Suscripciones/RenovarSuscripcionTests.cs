using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using BillingSaaS.Application.Common.Exceptions;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Application.Suscripciones.Commands.AprobarRenovacion;
using BillingSaaS.Application.Suscripciones.Commands.RechazarRenovacion;
using BillingSaaS.Application.Suscripciones.Commands.RenovarSuscripcion;
using BillingSaaS.Application.Suscripciones.Commands.SolicitarRenovacion;
using BillingSaaS.Application.Suscripciones.Queries.GetTenantSubscription;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Infrastructure.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.UnitTests.Suscripciones;

[TestFixture]
public class RenovarSuscripcionTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private Mock<IUser> _userMock = null!;
    private Mock<ISender> _senderMock = null!;
    private Mock<TimeProvider> _timeProviderMock = null!;
    private readonly DateTime _now = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
    private readonly Guid _tenantId = Guid.NewGuid();

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
        _userMock.Setup(u => u.TenantId).Returns(_tenantId);

        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(_now));

        _senderMock = new Mock<ISender>();
        _senderMock.Setup(s => s.Send(It.IsAny<GetTenantSubscriptionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSubscriptionDto());
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task RenovarSuscripcion_MismoPlan_DebeExtenderVigencia()
    {
        // Arrange
        var plan = Plan.Crear("COMERCIO_PRO", "Comercio Pro", "Ilimitado", 10m, 99m, null, null, 1, "01,04,05");
        _context.Planes.Add(plan);
        await _context.SaveChangesAsync();

        var fechaInicio = _now.AddDays(-20);
        var fechaVencimientoActual = _now.AddDays(10);
        var suscripcion = TenantSubscription.Crear(
            _tenantId,
            plan.Id,
            fechaInicio,
            fechaVencimientoActual,
            "MENSUAL"
        );
        _context.Suscripciones.Add(suscripcion);
        await _context.SaveChangesAsync();

        var handler = new RenovarSuscripcionCommandHandler(_context, _userMock.Object, _senderMock.Object, _timeProviderMock.Object);
        var command = new RenovarSuscripcionCommand
        {
            PlanId = plan.Id,
            Frecuencia = "MENSUAL"
        };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var subActualizada = await _context.Suscripciones.FirstAsync(s => s.TenantId == _tenantId);
        subActualizada.FechaVencimiento.ShouldBe(fechaVencimientoActual.AddMonths(1));
        subActualizada.Estado.ShouldBe("ACTIVO");
    }

    [Test]
    public async Task RenovarSuscripcion_CambioDePlan_DebeActualizarPlanYReiniciarCiclo()
    {
        // Arrange
        var planActual = Plan.Crear("EMPRENDEDOR", "Emprendedor", "30 docs", 5m, 50m, 30, null, 1, "01");
        var nuevoPlan = Plan.Crear("COMERCIO_PRO", "Comercio Pro", "Ilimitado", 10m, 99m, null, null, 1, "01,04,05");
        _context.Planes.AddRange(planActual, nuevoPlan);
        await _context.SaveChangesAsync();

        var fechaInicio = _now.AddDays(-25);
        var fechaVencimiento = _now.AddDays(5);
        var suscripcion = TenantSubscription.Crear(
            _tenantId,
            planActual.Id,
            fechaInicio,
            fechaVencimiento,
            "MENSUAL"
        );
        _context.Suscripciones.Add(suscripcion);
        await _context.SaveChangesAsync();

        var handler = new RenovarSuscripcionCommandHandler(_context, _userMock.Object, _senderMock.Object, _timeProviderMock.Object);
        var command = new RenovarSuscripcionCommand
        {
            PlanId = nuevoPlan.Id,
            Frecuencia = "ANUAL"
        };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var subActualizada = await _context.Suscripciones.FirstAsync(s => s.TenantId == _tenantId);
        subActualizada.PlanId.ShouldBe(nuevoPlan.Id);
        subActualizada.Frecuencia.ShouldBe("ANUAL");
        subActualizada.FechaInicio.ShouldBe(_now);
        subActualizada.FechaVencimiento.ShouldBe(_now.AddYears(1));
    }

    [Test]
    public async Task RenovarSuscripcion_PlanInexistente_DebeLanzarNotFoundException()
    {
        var handler = new RenovarSuscripcionCommandHandler(_context, _userMock.Object, _senderMock.Object, _timeProviderMock.Object);
        var command = new RenovarSuscripcionCommand
        {
            PlanId = 9999,
            Frecuencia = "MENSUAL"
        };

        await Should.ThrowAsync<NotFoundException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Test]
    public async Task RenovarSuscripcion_SinSuscripcionPrevia_DebeCrearNuevaSuscripcion()
    {
        var plan = Plan.Crear("PYME_MULTI", "PYME Multi-Sucursal", "Multisucursal", 25m, 240m, null, null, 5, "01,04,05,06,07");
        _context.Planes.Add(plan);
        await _context.SaveChangesAsync();

        var handler = new RenovarSuscripcionCommandHandler(_context, _userMock.Object, _senderMock.Object, _timeProviderMock.Object);
        var command = new RenovarSuscripcionCommand
        {
            PlanId = plan.Id,
            Frecuencia = "MENSUAL"
        };

        await handler.Handle(command, CancellationToken.None);

        var sub = await _context.Suscripciones.FirstOrDefaultAsync(s => s.TenantId == _tenantId);
        sub.ShouldNotBeNull();
        sub.PlanId.ShouldBe(plan.Id);
        sub.FechaInicio.ShouldBe(_now);
        sub.FechaVencimiento.ShouldBe(_now.AddMonths(1));
    }

    [Test]
    public async Task SolicitarRenovacion_DatosValidos_DebeCrearSolicitudPendiente()
    {
        var plan = Plan.Crear("COMERCIO_PRO", "Comercio Pro", "Ilimitado", 10m, 99m, null, null, 1, "01,04,05");
        _context.Planes.Add(plan);
        await _context.SaveChangesAsync();

        var handler = new SolicitarRenovacionCommandHandler(_context, _userMock.Object, _timeProviderMock.Object);
        var command = new SolicitarRenovacionCommand
        {
            PlanId = plan.Id,
            Frecuencia = "ANUAL",
            NumeroComprobante = "TRANSF-987654",
            BancoOrigen = "Banco Pichincha"
        };

        var resultado = await handler.Handle(command, CancellationToken.None);

        resultado.ShouldNotBeNull();
        resultado.Estado.ShouldBe("PENDIENTE");
        resultado.Monto.ShouldBe(99m);
        resultado.NumeroComprobante.ShouldBe("TRANSF-987654");

        var solicitudEnDb = await _context.SolicitudesRenovacion.FirstOrDefaultAsync(s => s.TenantId == _tenantId);
        solicitudEnDb.ShouldNotBeNull();
        solicitudEnDb.Estado.ShouldBe("PENDIENTE");
        solicitudEnDb.NumeroComprobante.ShouldBe("TRANSF-987654");
    }

    [Test]
    public async Task AprobarRenovacion_SolicitudValida_DebeActivarSuscripcionYAplicarLimites()
    {
        var plan = Plan.Crear("COMERCIO_PRO", "Comercio Pro", "Ilimitado", 10m, 99m, null, null, 1, "01,04,05");
        _context.Planes.Add(plan);
        await _context.SaveChangesAsync();

        var solicitud = SolicitudRenovacion.Crear(
            _tenantId,
            plan.Id,
            "MENSUAL",
            10m,
            "COMP-112233",
            _now
        );
        _context.SolicitudesRenovacion.Add(solicitud);
        await _context.SaveChangesAsync();

        var handler = new AprobarRenovacionCommandHandler(_context, _senderMock.Object, _timeProviderMock.Object);
        var result = await handler.Handle(new AprobarRenovacionCommand(solicitud.Id), CancellationToken.None);

        result.ShouldNotBeNull();
        result.PlanCodigo.ShouldBe("COMERCIO_PRO");
        result.Estado.ShouldBe("ACTIVO");

        var solicitudActualizada = await _context.SolicitudesRenovacion.FindAsync(solicitud.Id);
        solicitudActualizada!.Estado.ShouldBe("APROBADA");

        var subActualizada = await _context.Suscripciones.FirstOrDefaultAsync(s => s.TenantId == _tenantId);
        subActualizada.ShouldNotBeNull();
        subActualizada.PlanId.ShouldBe(plan.Id);
        subActualizada.Estado.ShouldBe("ACTIVO");
        subActualizada.FechaVencimiento.ShouldBe(_now.AddMonths(1));
    }

    [Test]
    public async Task RechazarRenovacion_DebeMarcarComoRechazadaConMotivo()
    {
        var plan = Plan.Crear("COMERCIO_PRO", "Comercio Pro", "Ilimitado", 10m, 99m, null, null, 1, "01,04,05");
        _context.Planes.Add(plan);
        await _context.SaveChangesAsync();

        var solicitud = SolicitudRenovacion.Crear(
            _tenantId,
            plan.Id,
            "MENSUAL",
            10m,
            "COMP-INVALIDO",
            _now
        );
        _context.SolicitudesRenovacion.Add(solicitud);
        await _context.SaveChangesAsync();

        var handler = new RechazarRenovacionCommandHandler(_context, _timeProviderMock.Object);
        await handler.Handle(new RechazarRenovacionCommand(solicitud.Id, "No se encontró la transferencia en la cuenta bancaria."), CancellationToken.None);

        var solicitudDb = await _context.SolicitudesRenovacion.FindAsync(solicitud.Id);
        solicitudDb!.Estado.ShouldBe("RECHAZADA");
        solicitudDb.MotivoRechazo.ShouldBe("No se encontró la transferencia en la cuenta bancaria.");
    }
}
