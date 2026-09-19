using BillingSaaS.Application.Suscripciones.Commands.AprobarRenovacion;
using BillingSaaS.Application.Suscripciones.Commands.RechazarRenovacion;
using BillingSaaS.Application.Suscripciones.Commands.RenovarSuscripcion;
using BillingSaaS.Application.Suscripciones.Commands.SolicitarRenovacion;
using BillingSaaS.Application.Suscripciones.Queries.GetPlanes;
using BillingSaaS.Application.Suscripciones.Queries.GetSolicitudesRenovacion;
using BillingSaaS.Application.Suscripciones.Queries.GetTenantSubscription;
using BillingSaaS.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BillingSaaS.Web.Endpoints;

public class Suscripciones : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/suscripcion";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        // Métricas y consulta
        groupBuilder.MapGet(GetActual, "actual").RequireAuthorization();
        groupBuilder.MapGet(GetPlanes, "planes");

        // Solicitud de renovación del cliente (reporte de transferencia / comprobante)
        groupBuilder.MapPost(SolicitarRenovacion, "renovar").RequireAuthorization();
        // groupBuilder.MapPost(SolicitarRenovacion, "solicitar-renovacion").RequireAuthorization();

        // Activación directa (para integración o testing)
        groupBuilder.MapPost(RenovarDirecto, "activar-directo").RequireAuthorization();

        // Endpoints administrativos para validar y aprobar pagos por transferencia
        groupBuilder.MapGet(GetSolicitudes, "solicitudes")
            .RequireAuthorization(new AuthorizeAttribute { Roles = Roles.Administrator });

        groupBuilder.MapPost(AprobarSolicitud, "solicitudes/{id:int}/aprobar")
            .RequireAuthorization(new AuthorizeAttribute { Roles = Roles.Administrator });

        groupBuilder.MapPost(RechazarSolicitud, "solicitudes/{id:int}/rechazar")
            .RequireAuthorization(new AuthorizeAttribute { Roles = Roles.Administrator });
    }

    [EndpointSummary("Obtener suscripción y métricas de consumo actual del Tenant")]
    [EndpointDescription("Devuelve el plan activo, vigencia, días restantes, periodo de gracia, consumo detallado y estado de solicitud de pago pendiente.")]
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

    [EndpointSummary("Registrar solicitud de renovación con comprobante de transferencia")]
    [EndpointDescription("El cliente envía los datos de su transferencia para que el administrador la valide y active/extienda su plan.")]
    public static async Task<Ok<SolicitudRenovacionDto>> SolicitarRenovacion(ISender sender, SolicitarRenovacionCommand command)
    {
        var response = await sender.Send(command);
        return TypedResults.Ok(response);
    }

    [EndpointSummary("Activación directa de suscripción (inmediata)")]
    [EndpointDescription("Activa o actualiza la suscripción de forma inmediata sin esperar aprobación de comprobante.")]
    public static async Task<Ok<TenantSubscriptionDto>> RenovarDirecto(ISender sender, RenovarSuscripcionCommand command)
    {
        var response = await sender.Send(command);
        return TypedResults.Ok(response);
    }

    [EndpointSummary("Admin: Listar solicitudes de renovación")]
    [EndpointDescription("Permite al administrador ver las solicitudes pendientes, aprobadas o rechazadas de todos los clientes.")]
    public static async Task<Ok<List<SolicitudRenovacionDto>>> GetSolicitudes(ISender sender, [FromQuery] string? estado)
    {
        var response = await sender.Send(new GetSolicitudesRenovacionQuery(estado));
        return TypedResults.Ok(response);
    }

    [EndpointSummary("Admin: Aprobar solicitud de renovación")]
    [EndpointDescription("El administrador confirma la recepción de la transferencia. Se activa o extiende el plan y se aplican límites de inmediato.")]
    public static async Task<Ok<TenantSubscriptionDto>> AprobarSolicitud(ISender sender, int id)
    {
        var response = await sender.Send(new AprobarRenovacionCommand(id));
        return TypedResults.Ok(response);
    }

    public record RechazarRequest(string Motivo);

    [EndpointSummary("Admin: Rechazar solicitud de renovación")]
    [EndpointDescription("El administrador rechaza el comprobante indicando el motivo para que el cliente lo corrija.")]
    public static async Task<Ok> RechazarSolicitud(ISender sender, int id, [FromBody] RechazarRequest request)
    {
        await sender.Send(new RechazarRenovacionCommand(id, request.Motivo));
        return TypedResults.Ok();
    }
}
