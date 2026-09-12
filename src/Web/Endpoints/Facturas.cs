using BillingSaaS.Application.Facturas.Commands.EmitirFactura;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BillingSaaS.Web.Endpoints;

public class Facturas : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();

        groupBuilder.MapPost(EmitirFactura);
    }

    [EndpointSummary("Emitir y firmar Factura Electrónica SRI")]
    [EndpointDescription("Genera el comprobante electrónico v1.1.0, lo firma digitalmente con el certificado del Emisor y lo envía al servicio web de recepción del SRI.")]
    public static async Task<Ok<EmitirFacturaResponseDto>> EmitirFactura(ISender sender, EmitirFacturaCommand command)
    {
        var response = await sender.Send(command);

        return TypedResults.Ok(response);
    }
}

