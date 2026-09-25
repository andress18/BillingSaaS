using BillingSaaS.Application.Clientes.Commands.ActualizarCliente;
using BillingSaaS.Application.Clientes.Commands.DesactivarCliente;
using BillingSaaS.Application.Clientes.Commands.UpsertCliente;
using BillingSaaS.Application.Clientes.Queries.GetClienteById;
using BillingSaaS.Application.Clientes.Queries.SearchClientes;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.UnitTests.Clientes;

[TestFixture]
public class ClientesCqrsTests
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
    public async Task UpsertCliente_CuandoNoExiste_DebeCrearNuevoCliente()
    {
        var handler = new UpsertClienteCommandHandler(_context, _userMock.Object);

        var command = new UpsertClienteCommand
        {
            TipoIdentificacion = "04",
            Identificacion = "1790012345001",
            RazonSocial = "FARMACIA SAN PEDRO CIA LTDA",
            Direccion = "Av. Amazonas 123",
            CorreoElectronico = "info@sanpedro.com"
        };

        var id = await handler.Handle(command, CancellationToken.None);

        id.ShouldNotBe(Guid.Empty);

        var clienteEnDb = await _context.CatalogoClientes.FindAsync([id], CancellationToken.None);
        clienteEnDb.ShouldNotBeNull();
        clienteEnDb.RazonSocial.ShouldBe("FARMACIA SAN PEDRO CIA LTDA");
        clienteEnDb.TenantId.ShouldBe(_tenantId);
        clienteEnDb.Activo.ShouldBeTrue();
    }

    [Test]
    public async Task UpsertCliente_CuandoYaExiste_DebeActualizarDatosDeContacto()
    {
        var clienteExistente = CatalogoCliente.Crear(
            _tenantId,
            "04",
            "1790012345001",
            "NOMBRE ANTIGUO",
            "Dir Antigua",
            "antiguo@correo.com");

        _context.CatalogoClientes.Add(clienteExistente);
        await _context.SaveChangesAsync();

        var handler = new UpsertClienteCommandHandler(_context, _userMock.Object);

        var command = new UpsertClienteCommand
        {
            TipoIdentificacion = "04",
            Identificacion = "1790012345001",
            RazonSocial = "NOMBRE ACTUALIZADO S.A.",
            Direccion = "Nueva Direccion",
            CorreoElectronico = "nuevo@correo.com"
        };

        var id = await handler.Handle(command, CancellationToken.None);

        id.ShouldBe(clienteExistente.Id);

        var clienteEnDb = await _context.CatalogoClientes.FindAsync([id], CancellationToken.None);
        clienteEnDb.ShouldNotBeNull();
        clienteEnDb.RazonSocial.ShouldBe("NOMBRE ACTUALIZADO S.A.");
        clienteEnDb.Direccion.ShouldBe("Nueva Direccion");
        clienteEnDb.CorreoElectronico.ShouldBe("nuevo@correo.com");
    }

    [Test]
    public async Task SearchClientes_DebeFiltrarPorTerminoYRespetarTenant()
    {
        var c1 = CatalogoCliente.Crear(_tenantId, "04", "1790012345001", "DISTRIBUIDORA ALFA", "Quito");
        var c2 = CatalogoCliente.Crear(_tenantId, "05", "1712345678", "JUAN ALFA", "Guayaquil");
        var c3 = CatalogoCliente.Crear(_tenantId, "04", "0990099001001", "BETA S.A.", "Cuenca");
        var otroTenantId = Guid.NewGuid();
        var cOtro = CatalogoCliente.Crear(otroTenantId, "04", "1790012345001", "ALFA OTRO TENANT");

        _context.CatalogoClientes.AddRange(c1, c2, c3, cOtro);
        await _context.SaveChangesAsync();

        var handler = new SearchClientesQueryHandler(_context, _userMock.Object);

        var result = await handler.Handle(new SearchClientesQuery("ALFA", Limit: 10), CancellationToken.None);

        result.Count.ShouldBe(2);
        result.Any(c => c.Id == c1.Id).ShouldBeTrue();
        result.Any(c => c.Id == c2.Id).ShouldBeTrue();
        result.Any(c => c.Id == cOtro.Id).ShouldBeFalse();
    }

    [Test]
    public async Task GetClienteById_DebeRetornarClienteCorrecto()
    {
        var cliente = CatalogoCliente.Crear(_tenantId, "04", "1790012345001", "EMPRESA TEST");
        _context.CatalogoClientes.Add(cliente);
        await _context.SaveChangesAsync();

        var handler = new GetClienteByIdQueryHandler(_context, _userMock.Object);
        var result = await handler.Handle(new GetClienteByIdQuery(cliente.Id), CancellationToken.None);

        result.ShouldNotBeNull();
        result.RazonSocial.ShouldBe("EMPRESA TEST");
    }

    [Test]
    public async Task DesactivarCliente_DebeHacerSoftDeleteYExcluirloDeBusquedas()
    {
        var cliente = CatalogoCliente.Crear(_tenantId, "04", "1790012345001", "CLIENTE A DESACTIVAR");
        _context.CatalogoClientes.Add(cliente);
        await _context.SaveChangesAsync();

        var desactivarHandler = new DesactivarClienteCommandHandler(_context, _userMock.Object);
        await desactivarHandler.Handle(new DesactivarClienteCommand(cliente.Id), CancellationToken.None);

        var clienteEnDb = await _context.CatalogoClientes.FindAsync([cliente.Id], CancellationToken.None);
        clienteEnDb.ShouldNotBeNull();
        clienteEnDb.Activo.ShouldBeFalse();

        // Verificar que el autocompletado no lo devuelve
        var searchHandler = new SearchClientesQueryHandler(_context, _userMock.Object);
        var searchResults = await searchHandler.Handle(new SearchClientesQuery("DESACTIVAR"), CancellationToken.None);
        searchResults.ShouldBeEmpty();
    }

    [Test]
    public async Task UpsertCliente_CuandoAlcanzaLimite10Clientes_DebeLanzarInvalidOperationException()
    {
        var handler = new UpsertClienteCommandHandler(_context, _userMock.Object);

        // Registrar 10 clientes previos
        for (int i = 1; i <= 10; i++)
        {
            var c = CatalogoCliente.Crear(_tenantId, "04", $"179001234500{i:D1}", $"CLIENTE {i}");
            _context.CatalogoClientes.Add(c);
        }
        await _context.SaveChangesAsync();

        // Intentar registrar el cliente número 11
        var command = new UpsertClienteCommand
        {
            TipoIdentificacion = "04",
            Identificacion = "1790012345099",
            RazonSocial = "CLIENTE ONCE S.A.",
            Direccion = "Av. 11",
            CorreoElectronico = "once@test.com"
        };

        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            handler.Handle(command, CancellationToken.None));

        ex.Message.ShouldContain("límite máximo permitido de 10 compradores");
    }
}

