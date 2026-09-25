using BillingSaaS.Application.Productos.Commands.ActualizarProducto;
using BillingSaaS.Application.Productos.Commands.CrearProducto;
using BillingSaaS.Application.Productos.Commands.DesactivarProducto;
using BillingSaaS.Application.Productos.Queries.GetProductoById;
using BillingSaaS.Application.Productos.Queries.GetProductos;
using BillingSaaS.Application.Productos.Queries.SearchProductos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BillingSaaS.Web.Endpoints;

public class Productos : IEndpointGroup
{
    public static string? RoutePrefix => "/api/productos";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();

        groupBuilder.MapGet(GetProductos, "");
        groupBuilder.MapGet(SearchProductos, "search");
        groupBuilder.MapGet(GetProductoById, "{id:guid}");
        groupBuilder.MapPost(CrearProducto);
        groupBuilder.MapPut(ActualizarProducto, "{id:guid}");
        groupBuilder.MapDelete(DesactivarProducto, "{id:guid}");
    }

    [EndpointSummary("Listar todos los productos y servicios del catálogo")]
    [EndpointDescription("Obtiene la lista completa de ítems (productos y servicios) del catálogo del tenant con soporte de filtrado opcional.")]
    public static async Task<Ok<List<ProductoLookupDto>>> GetProductos(
        ISender sender,
        [FromQuery(Name = "q")] string? q,
        [FromQuery] bool soloActivos = true)
    {
        var response = await sender.Send(new GetProductosQuery(q, soloActivos));
        return TypedResults.Ok(response);
    }

    [EndpointSummary("Búsqueda rápida y autocompletado de productos/servicios")]
    [EndpointDescription("Busca ítems en el catálogo maestro por código principal o descripción para autocompletar líneas de factura.")]
    public static async Task<Ok<List<ProductoLookupDto>>> SearchProductos(
        ISender sender,
        [FromQuery(Name = "q")] string? q,
        [FromQuery] int limit = 10)
    {
        var response = await sender.Send(new SearchProductosQuery(q, limit));
        return TypedResults.Ok(response);
    }

    [EndpointSummary("Obtener producto por ID")]
    [EndpointDescription("Obtiene los datos de un ítem del catálogo maestro por su identificador único.")]
    public static async Task<Results<Ok<ProductoLookupDto>, NotFound>> GetProductoById(ISender sender, Guid id)
    {
        var producto = await sender.Send(new GetProductoByIdQuery(id));
        return producto != null ? TypedResults.Ok(producto) : TypedResults.NotFound();
    }

    [EndpointSummary("Registrar producto o servicio")]
    [EndpointDescription("Registra un nuevo producto o servicio con sus tarifas de impuesto en el catálogo maestro.")]
    public static async Task<Created<Guid>> CrearProducto(ISender sender, CrearProductoCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/api/productos/{id}", id);
    }

    [EndpointSummary("Actualizar producto existente")]
    [EndpointDescription("Actualiza descripción, precio unitario o tarifas de impuesto de un ítem del catálogo maestro.")]
    public static async Task<IResult> ActualizarProducto(ISender sender, Guid id, ActualizarProductoCommand command)
    {
        if (id != command.Id)
        {
            return TypedResults.BadRequest("El ID de la ruta no coincide con el cuerpo de la petición.");
        }

        await sender.Send(command);
        return TypedResults.NoContent();
    }

    [EndpointSummary("Eliminar / archivar producto (Soft Delete)")]
    [EndpointDescription("Desactiva un producto del catálogo maestro para que no aparezca en las búsquedas ni sugerencias.")]
    public static async Task<IResult> DesactivarProducto(ISender sender, Guid id)
    {
        await sender.Send(new DesactivarProductoCommand(id));
        return TypedResults.NoContent();
    }
}

