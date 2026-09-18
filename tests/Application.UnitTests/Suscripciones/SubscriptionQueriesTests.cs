using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Application.Suscripciones.Queries.GetPlanes;
using BillingSaaS.Application.Suscripciones.Queries.GetTenantSubscription;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Domain.Exceptions;
using BillingSaaS.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.UnitTests.Suscripciones;

[TestFixture]
public class SubscriptionQueriesTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private Mock<IUser> _userMock = null!;
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
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task GetPlanesQuery_SoloDebeRetornarPlanesPublicosYActivos()
    {
        // 1 plan público
        var planPublico = Plan.Crear("COMERCIO_PRO", "Comercio Pro", "Ilimitado", 10m, 99m, null, null, 1, "01,04,05", esPublico: true);
        // 1 plan privado (LEGACY)
        var planLegado = Plan.Crear("LEGACY", "Plan Legado", "Oculto", 0m, 45m, null, 300, 1, "01,04", esPublico: false);
        // 1 plan inactivo
        var planInactivo = Plan.Crear("INACTIVO", "Plan Inactivo", "Desc", 1m, 10m, null, null, 1, "01", esPublico: true);
        planInactivo.Desactivar();

        _context.Planes.AddRange(planPublico, planLegado, planInactivo);
        await _context.SaveChangesAsync();

        var handler = new GetPlanesQueryHandler(_context);
        var result = await handler.Handle(new GetPlanesQuery(), CancellationToken.None);

        result.Count.ShouldBe(1);
        result.First().Codigo.ShouldBe("COMERCIO_PRO");
        result.First().Destacado.ShouldBeTrue();
    }

    [Test]
    public async Task GetTenantSubscriptionQuery_ConSuscripcionActiva_DebeCalcularMetricasCorrectas()
    {
        var plan = Plan.Crear("EMPRENDEDOR", "Emprendedor", "30 facturas", 5m, 50m, 30, 360, 1, "01,04", true);
        _context.Planes.Add(plan);
        await _context.SaveChangesAsync();

        var sub = TenantSubscription.Crear(
            _tenantId,
            plan.Id,
            _now.AddDays(-10),
            _now.AddDays(20),
            "MENSUAL"
        );
        _context.Suscripciones.Add(sub);

        var emisor = Emisor.Crear(_tenantId, "0957790108001", "Mi Farmacia", "Matriz", codigoEstablecimiento: "001");
        _context.Emisores.Add(emisor);
        await _context.SaveChangesAsync();

        // Crear 6 facturas emitidas
        for (int i = 1; i <= 6; i++)
        {
            var f = Factura.Crear(_tenantId, 1, "Mi Farmacia", "0957790108001", "001", "001", $"{i:D9}", "Matriz", _now.Date,
                Comprador.Crear("07", "9999999999999", "CF", "Dir"), [], emisor.Id);
            f.MarcarComoAutorizada($"1234567890123456789012345678901234567890123456{i:D3}", _now);
            _context.Facturas.Add(f);
        }
        await _context.SaveChangesAsync();

        var handler = new GetTenantSubscriptionQueryHandler(_context, _userMock.Object, _timeProviderMock.Object);
        var result = await handler.Handle(new GetTenantSubscriptionQuery(), CancellationToken.None);

        result.PlanNombre.ShouldBe("Emprendedor");
        result.PlanCodigo.ShouldBe("EMPRENDEDOR");
        result.EsIlimitado.ShouldBeFalse();
        result.DocumentosEmitidos.ShouldBe(6);
        result.DocumentosMaximos.ShouldBe(30);
        result.DocumentosDisponibles.ShouldBe(24);
        result.PorcentajeUso.ShouldBe(20.0m);
        result.DiasRestantes.ShouldBe(20);
        result.EnPeriodoGracia.ShouldBeFalse();
        result.MaxEstablecimientos.ShouldBe(1);
        result.EstablecimientosRegistrados.ShouldBe(1);
    }

    [Test]
    public async Task GetTenantSubscriptionQuery_SinSuscripcion_DebeLanzarSubscriptionRequiredException()
    {
        var handler = new GetTenantSubscriptionQueryHandler(_context, _userMock.Object, _timeProviderMock.Object);

        await Should.ThrowAsync<SubscriptionRequiredException>(() =>
            handler.Handle(new GetTenantSubscriptionQuery(), CancellationToken.None));
    }
}
