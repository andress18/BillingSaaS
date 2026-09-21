using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Application.Productos.Commands.ActualizarProducto;
using BillingSaaS.Application.Productos.Commands.CrearProducto;
using BillingSaaS.Application.Productos.Queries.GetProductoById;
using BillingSaaS.Application.Productos.Queries.SearchProductos;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.UnitTests.Productos;

[TestFixture]
public class ProductosCqrsTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private Mock<IUser> _userMock = null!;
    private readonly Guid _tenantId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _userMock = new Mock<IUser>();
        _userMock.Setup(u => u.TenantId).Returns(_tenantId);

        _context = new ApplicationDbContext(options, _userMock.Object);
        _context.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task CrearProducto_ConDatosValidos_DebeCrearProducto()
    {
        var handler = new CrearProductoCommandHandler(_context, _userMock.Object);

        var command = new CrearProductoCommand
        {
            CodigoPrincipal = "SERV-TI-01",
            Descripcion = "Consultoría de Software",
            PrecioUnitario = 150.00m,
            CodigoImpuesto = "2",
            CodigoPorcentaje = "4",
            Tarifa = 15.00m
        };

        var id = await handler.Handle(command, CancellationToken.None);

        id.ShouldNotBe(Guid.Empty);

        var productoEnDb = await _context.CatalogoProductos.FindAsync([id], CancellationToken.None);
        productoEnDb.ShouldNotBeNull();
        productoEnDb.CodigoPrincipal.ShouldBe("SERV-TI-01");
        productoEnDb.Descripcion.ShouldBe("Consultoría de Software");
        productoEnDb.PrecioUnitario.ShouldBe(150.00m);
        productoEnDb.TenantId.ShouldBe(_tenantId);
    }

    [Test]
    public async Task CrearProducto_ConCodigoDuplicadoEnMismoTenant_DebeLanzarExcepcion()
    {
        var productoExistente = CatalogoProducto.Crear(_tenantId, "PROD-REP", "Producto Inicial", 10m);
        _context.CatalogoProductos.Add(productoExistente);
        await _context.SaveChangesAsync();

        var handler = new CrearProductoCommandHandler(_context, _userMock.Object);

        var command = new CrearProductoCommand
        {
            CodigoPrincipal = "PROD-REP",
            Descripcion = "Producto Duplicado",
            PrecioUnitario = 20m
        };

        await Should.ThrowAsync<InvalidOperationException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Test]
    public async Task SearchProductos_DebeFiltrarPorCodigoODescripcion()
    {
        var p1 = CatalogoProducto.Crear(_tenantId, "MED-01", "Paracetamol 500mg", 0.50m);
        var p2 = CatalogoProducto.Crear(_tenantId, "MED-02", "Ibuprofeno 400mg", 0.75m);
        var p3 = CatalogoProducto.Crear(_tenantId, "ACC-01", "Jeringa Descartable", 0.25m);

        _context.CatalogoProductos.AddRange(p1, p2, p3);
        await _context.SaveChangesAsync();

        var handler = new SearchProductosQueryHandler(_context, _userMock.Object);

        var result = await handler.Handle(new SearchProductosQuery("MED", Limit: 10), CancellationToken.None);

        result.Count.ShouldBe(2);
        result.Any(p => p.Id == p1.Id).ShouldBeTrue();
        result.Any(p => p.Id == p2.Id).ShouldBeTrue();
    }

    [Test]
    public async Task ActualizarProducto_DebeModificarValores()
    {
        var producto = CatalogoProducto.Crear(_tenantId, "SERV-01", "Servicio Base", 50m);
        _context.CatalogoProductos.Add(producto);
        await _context.SaveChangesAsync();

        var handler = new ActualizarProductoCommandHandler(_context, _userMock.Object);

        await handler.Handle(new ActualizarProductoCommand
        {
            Id = producto.Id,
            Descripcion = "Servicio Premium",
            PrecioUnitario = 95.00m,
            CodigoImpuesto = "2",
            CodigoPorcentaje = "4",
            Tarifa = 15m,
            Activo = true
        }, CancellationToken.None);

        var actualizado = await _context.CatalogoProductos.FindAsync([producto.Id], CancellationToken.None);
        actualizado.ShouldNotBeNull();
        actualizado.Descripcion.ShouldBe("Servicio Premium");
        actualizado.PrecioUnitario.ShouldBe(95.00m);
    }
}

