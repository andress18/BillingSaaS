using BillingSaaS.Application.Clientes.Commands.ActualizarCliente;
using BillingSaaS.Application.Clientes.Commands.UpsertCliente;
using BillingSaaS.Application.Clientes.Queries.GetClienteById;
using BillingSaaS.Application.Clientes.Queries.SearchClientes;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BillingSaaS.Web.Endpoints;

public class Clientes : IEndpointGroup
{
    public static string? RoutePrefix => "/api/clientes";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();

        groupBuilder.MapGet(SearchClientes, "search");
        groupBuilder.MapGet(GetClienteById, "{id:guid}");
        groupBuilder.MapPost(UpsertCliente);
        groupBuilder.MapPut(ActualizarCliente, "{id:guid}");
    }

    [EndpointSummary("Búsqueda rápida y autocompletado de clientes")]
    [EndpointDescription("Busca clientes en el catálogo maestro por identificación o razón social para autocompletar la emisión.")]
    public static async Task<Ok<List<ClienteLookupDto>>> SearchClientes(
        ISender sender,
        [FromQuery(Name = "q")] string? q,
        [FromQuery] int limit = 10)
    {
        var response = await sender.Send(new SearchClientesQuery(q, limit));
        return TypedResults.Ok(response);
    }

    [EndpointSummary("Obtener cliente por ID")]
    [EndpointDescription("Obtiene los datos de un cliente del catálogo maestro por su identificador único.")]
    public static async Task<Results<Ok<ClienteLookupDto>, NotFound>> GetClienteById(ISender sender, Guid id)
    {
        var cliente = await sender.Send(new GetClienteByIdQuery(id));
        return cliente != null ? TypedResults.Ok(cliente) : TypedResults.NotFound();
    }

    [EndpointSummary("Crear o sincronizar cliente (Upsert)")]
    [EndpointDescription("Crea un nuevo cliente en el catálogo maestro o actualiza sus datos de contacto si la identificación ya existe en el tenant.")]
    public static async Task<Ok<Guid>> UpsertCliente(ISender sender, UpsertClienteCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Ok(id);
    }

    [EndpointSummary("Actualizar cliente existente")]
    [EndpointDescription("Actualiza los datos de contacto y estado de un cliente en el catálogo maestro.")]
    public static async Task<IResult> ActualizarCliente(ISender sender, Guid id, ActualizarClienteCommand command)
    {
        if (id != command.Id)
        {
            return TypedResults.BadRequest("El ID de la ruta no coincide con el cuerpo de la petición.");
        }

        await sender.Send(command);
        return TypedResults.NoContent();
    }
}

