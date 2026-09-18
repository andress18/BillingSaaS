using BillingSaaS.Application.Suscripciones.Queries.GetPlanes;
using BillingSaaS.Application.Suscripciones.Queries.GetTenantSubscription;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BillingSaaS.Web.Endpoints;

public class Suscripciones : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/suscripcion";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet(GetActual, "actual").RequireAuthorization();
        groupBuilder.MapGet(GetPlanes, "planes");
    }

    [EndpointSummary("Obtener suscripción y métricas de consumo actual del Tenant")]
    [EndpointDescription("Devuelve el plan activo, vigencia, días restantes, periodo de gracia y consumo detallado de documentos y sucursales.")]
    public static async Task<Ok<TenantSubscriptionDto>> GetActual(ISender sender)
    {
        var response = await sender.Send(new GetTenantSubscriptionQuery());

        return TypedResults.Ok(response);
    }

    [EndpointSummary("Listar planes de suscripción disponibles")]
    [EndpointDescription("Obtiene el catálogo público de planes con sus precios (mensual/anual), límites y características.")]
    public static async Task<Ok<List<PlanDto>>> GetPlanes(ISender sender)
    {
        var response = await sender.Send(new GetPlanesQuery());

        return TypedResults.Ok(response);
    }
}

