using BillingSaaS.Application.Emisores.Commands.ConfigurarEmisor;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BillingSaaS.Web.Endpoints;

public class Emisores : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapPost(ConfigurarEmisor);
    }

    [EndpointSummary("Configurar Emisor y Certificado Digital")]
    [EndpointDescription("Registra un nuevo emisor tributario con sus datos de establecimiento y certificado .p12 para facturación electrónica.")]
    public static async Task<Created<int>> ConfigurarEmisor(ISender sender, ConfigurarEmisorCommand command)
    {
        var id = await sender.Send(command);

        return TypedResults.Created($"/api/Emisores/{id}", id);
    }
}

