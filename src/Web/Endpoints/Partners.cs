using BillingSaaS.Application.Tenants.Commands.ActivarTenant;
using BillingSaaS.Application.Tenants.Commands.OnboardingCliente;
using BillingSaaS.Application.Tenants.Queries.GetClientesCartera;
using BillingSaaS.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BillingSaaS.Web.Endpoints;

public class Partners : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/partner";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        // Requiere rol Administrator o Partner
        groupBuilder.RequireAuthorization(new AuthorizeAttribute { Roles = $"{Roles.Administrator},{Roles.Partner}" });

        groupBuilder.MapPost(OnboardingCliente, "clientes/onboarding");
        groupBuilder.MapGet(GetClientesCartera, "clientes");

        // Endpoints de activación restringidos exclusivamente al SuperAdministrador
        groupBuilder.MapPost(ActivarCliente, "clientes/{tenantId:guid}/activar")
            .RequireAuthorization(new AuthorizeAttribute { Roles = Roles.Administrator });

        groupBuilder.MapPost(ActivarClientePorToken, "clientes/activar-por-token")
            .RequireAuthorization(new AuthorizeAttribute { Roles = Roles.Administrator });
    }

    [EndpointSummary("Alta rápida de nuevo cliente (Onboarding)")]
    [EndpointDescription("Crea atómicamente el Tenant (en estado PendientePago), Usuario cliente, Suscripción y opcionalmente el Emisor.")]
    public static async Task<Ok<OnboardingClienteResponseDto>> OnboardingCliente(ISender sender, OnboardingClienteCommand command)
    {
        var response = await sender.Send(command);
        return TypedResults.Ok(response);
    }

    [EndpointSummary("Listar clientes de la cartera")]
    [EndpointDescription("Devuelve los clientes del partner autenticado (o todos si quien consulta es Administrador), con estado de suscripción, caducidad de firma y ordenación configurable.")]
    public static async Task<Ok<List<ClienteCarteraDto>>> GetClientesCartera(
        ISender sender,
        [FromQuery] Guid? partnerId,
        [FromQuery] string? sortBy,
        [FromQuery] bool? descending)
    {
        var response = await sender.Send(new GetClientesCarteraQuery(partnerId, sortBy, descending ?? true));
        return TypedResults.Ok(response);
    }

    [EndpointSummary("Activar cliente por TenantId (SuperAdmin)")]
    [EndpointDescription("Cambia el estado del tenant a Activo, activa su suscripción por 365 días y habilita la facturación electrónica tras confirmar el pago.")]
    public static async Task<Ok<ActivarTenantResponseDto>> ActivarCliente(
        ISender sender,
        [FromRoute] Guid tenantId,
        [FromQuery] int? diasVigencia)
    {
        var response = await sender.Send(new ActivarTenantCommand
        {
            TenantId = tenantId,
            DiasVigencia = diasVigencia ?? 365
        });
        return TypedResults.Ok(response);
    }

    [EndpointSummary("Activar cliente por Token de Activación (SuperAdmin)")]
    [EndpointDescription("Permite activar un tenant utilizando su token único de activación.")]
    public static async Task<Ok<ActivarTenantResponseDto>> ActivarClientePorToken(
        ISender sender,
        [FromBody] ActivarPorTokenRequest request)
    {
        var response = await sender.Send(new ActivarTenantCommand
        {
            TokenActivacion = request.Token,
            DiasVigencia = request.DiasVigencia <= 0 ? 365 : request.DiasVigencia
        });
        return TypedResults.Ok(response);
    }
}

public record ActivarPorTokenRequest(string Token, int DiasVigencia = 365);

