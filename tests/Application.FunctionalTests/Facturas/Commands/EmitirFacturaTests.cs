using BillingSaaS.Application.Common.Exceptions;
using BillingSaaS.Application.Facturas.Commands.EmitirFactura;
using BillingSaaS.Domain.Entities;
using NUnit.Framework;
using Shouldly;

namespace BillingSaaS.Application.FunctionalTests.Facturas.Commands;

public class EmitirFacturaTests : TestBase
{
    [Test]
    public async Task EmitirFactura_EmisorNoExiste_DebeLanzarNotFoundException()
    {
        await TestApp.RunAsDefaultUserAsync();

        var command = new EmitirFacturaCommand
        {
            EmisorId = 99999, // Emisor inexistente
            Cliente = new EmitirFacturaCommand.CompradorDto(
                TipoIdentificacion: "07",
                Identificacion: "9999999999999",
                RazonSocial: "CONSUMIDOR FINAL",
                Direccion: "Quito",
                CorreoElectronico: null
            ),
            Detalles =
            [
                new EmitirFacturaCommand.DetalleDto(
                    CodigoPrincipal: "P01",
                    Descripcion: "Consulta Médica",
                    Cantidad: 1,
                    PrecioUnitario: 20m,
                    Descuento: 0,
                    Impuestos: [new EmitirFacturaCommand.ImpuestoDto("2", "4", 15m, 20m)]
                )
            ]
        };

        await Should.ThrowAsync<NotFoundException>(() => TestApp.SendAsync(command));
    }

    [Test]
    public async Task EmitirFactura_EmisorSinCertificado_DebeLanzarInvalidOperationException()
    {
        await TestApp.RunAsDefaultUserAsync();

        var emisor = Emisor.Crear(
            tenantId: Guid.NewGuid(),
            ruc: "0957790108001",
            razonSocial: "FARMACIA SAN JOSE",
            direccionMatriz: "Guayaquil"
        );

        await TestApp.AddAsync(emisor);

        var command = new EmitirFacturaCommand
        {
            EmisorId = emisor.Id,
            Cliente = new EmitirFacturaCommand.CompradorDto(
                TipoIdentificacion: "07",
                Identificacion: "9999999999999",
                RazonSocial: "CONSUMIDOR FINAL",
                Direccion: "Quito",
                CorreoElectronico: null
            ),
            Detalles =
            [
                new EmitirFacturaCommand.DetalleDto(
                    CodigoPrincipal: "P01",
                    Descripcion: "Consulta Médica",
                    Cantidad: 1,
                    PrecioUnitario: 20m,
                    Descuento: 0,
                    Impuestos: [new EmitirFacturaCommand.ImpuestoDto("2", "4", 15m, 20m)]
                )
            ]
        };

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => TestApp.SendAsync(command));
        ex.Message.ShouldContain("certificado digital");
    }
}

