using BillingSaaS.Application.Clientes.Queries.ConsultarSri;
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
public class ConsultarSriQueryTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private Mock<IUser> _userMock = null!;
    private Mock<ISriConsultaRucService> _sriServiceMock = null!;
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

        _sriServiceMock = new Mock<ISriConsultaRucService>();

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
    public async Task ConsultarSri_CuandoExisteEnCatalogoLocal_DebeRetornarDatosLocalesSinLlamarAlSri()
    {
        var clienteLocal = CatalogoCliente.Crear(_tenantId, "04", "1790012345001", "FARMACIA LOCAL S.A.");
        _context.CatalogoClientes.Add(clienteLocal);
        await _context.SaveChangesAsync();

        var handler = new ConsultarSriQueryHandler(_sriServiceMock.Object, _context, _userMock.Object);

        var result = await handler.Handle(new ConsultarSriQuery("1790012345001"), CancellationToken.None);

        result.ShouldNotBeNull();
        result.RazonSocial.ShouldBe("FARMACIA LOCAL S.A.");
        result.Identificacion.ShouldBe("1790012345001");
        result.TipoIdentificacion.ShouldBe("04");

        // No debe llamar al servicio externo si ya está en local
        _sriServiceMock.Verify(s => s.ConsultarPorIdentificacionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ConsultarSri_CuandoNoExisteEnCatalogoLocal_DebeLlamarAlServicioSri()
    {
        var expectedDto = new SriContribuyenteDto(
            Identificacion: "1790016919001",
            RazonSocial: "CORPORACION FAVORITA C.A.",
            TipoIdentificacion: "04",
            Estado: "ACTIVO"
        );

        _sriServiceMock
            .Setup(s => s.ConsultarPorIdentificacionAsync("1790016919001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var handler = new ConsultarSriQueryHandler(_sriServiceMock.Object, _context, _userMock.Object);

        var result = await handler.Handle(new ConsultarSriQuery("1790016919001"), CancellationToken.None);

        result.ShouldNotBeNull();
        result.RazonSocial.ShouldBe("CORPORACION FAVORITA C.A.");
        _sriServiceMock.Verify(s => s.ConsultarPorIdentificacionAsync("1790016919001", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ConsultarSri_ConIdentificacionVacia_DebeRetornarNull()
    {
        var handler = new ConsultarSriQueryHandler(_sriServiceMock.Object, _context, _userMock.Object);
        var result = await handler.Handle(new ConsultarSriQuery(""), CancellationToken.None);
        result.ShouldBeNull();
    }
}

