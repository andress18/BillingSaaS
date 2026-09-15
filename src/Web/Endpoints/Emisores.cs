using BillingSaaS.Application.Emisores.Commands.ConfigurarEmisor;
using BillingSaaS.Application.Emisores.Queries.GetEmisores;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BillingSaaS.Web.Endpoints;

public class Emisores : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();

        groupBuilder.MapGet(GetEmisores);
        groupBuilder.MapPost(ConfigurarEmisor);
    }

    [EndpointSummary("Listar Emisores del Tenant")]
    [EndpointDescription("Devuelve el listado de emisores tributarios disponibles para el tenant del usuario autenticado.")]
    public static async Task<Ok<List<EmisorBriefDto>>> GetEmisores(ISender sender, [AsParameters] GetEmisoresQuery query)
    {
        var response = await sender.Send(query);

        return TypedResults.Ok(response);
    }

    [EndpointSummary("Configurar Emisor y Certificado Digital")]
    [EndpointDescription("Registra un nuevo emisor tributario con sus datos de establecimiento y certificado .p12 para facturación electrónica.")]
    public static async Task<Created<int>> ConfigurarEmisor(ISender sender, ConfigurarEmisorCommand command)
    {
        var id = await sender.Send(command);

        return TypedResults.Created($"/api/Emisores/{id}", id);
    }
}

