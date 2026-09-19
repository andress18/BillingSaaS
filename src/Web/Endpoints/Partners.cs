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
    }

    [EndpointSummary("Alta rápida de nuevo cliente (Onboarding)")]
    [EndpointDescription("Crea atómicamente el Tenant, Usuario cliente, Suscripción anual activa y opcionalmente el Emisor con certificado digital .p12 cifrado.")]
    public static async Task<Ok<OnboardingClienteResponseDto>> OnboardingCliente(ISender sender, OnboardingClienteCommand command)
    {
        var response = await sender.Send(command);
        return TypedResults.Ok(response);
    }

    [EndpointSummary("Listar clientes de la cartera")]
    [EndpointDescription("Devuelve los clientes del partner autenticado (o todos si quien consulta es Administrador), con estado de suscripción y caducidad de firma.")]
    public static async Task<Ok<List<ClienteCarteraDto>>> GetClientesCartera(ISender sender, [FromQuery] Guid? partnerId)
    {
        var response = await sender.Send(new GetClientesCarteraQuery(partnerId));
        return TypedResults.Ok(response);
    }
}

