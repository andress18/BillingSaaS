using BillingSaaS.Application.Common.Models;
using BillingSaaS.Application.Common.Models.Sri;
using BillingSaaS.Application.NotasCredito.Commands.EmitirNotaCredito;
using BillingSaaS.Application.NotasCredito.Commands.EnviarNotaCreditoEmail;
using BillingSaaS.Application.NotasCredito.Queries.ConsultarAutorizacionNotaCredito;
using BillingSaaS.Application.NotasCredito.Queries.GetNotaCreditoPdf;
using BillingSaaS.Application.NotasCredito.Queries.GetNotaCreditoXml;
using BillingSaaS.Application.NotasCredito.Queries.GetNotasCredito;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BillingSaaS.Web.Endpoints;

public class NotasCredito : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();

        groupBuilder.MapPost(EmitirNotaCredito);
        groupBuilder.MapGet(GetNotasCredito);
        groupBuilder.MapGet(ConsultarAutorizacionNotaCredito, "{claveAcceso}/autorizacion");
        groupBuilder.MapGet(DescargarPdfNotaCredito, "{id:int}/pdf");
        groupBuilder.MapGet(DescargarXmlNotaCredito, "{id:int}/xml");
        groupBuilder.MapPost(EnviarCorreoNotaCredito, "{id:int}/enviar-correo");
    }

    [EndpointSummary("Listar notas de crédito electrónicas")]
    [EndpointDescription("Obtiene un listado paginado y filtrado de las notas de crédito emitidas por emisor, estado, comprador o rango de fechas.")]
    public static async Task<Ok<PaginatedList<NotaCreditoBriefDto>>> GetNotasCredito(ISender sender, [AsParameters] GetNotasCreditoQuery query)
    {
        var response = await sender.Send(query);

        return TypedResults.Ok(response);
    }

    [EndpointSummary("Emitir y firmar Nota de Crédito Electrónica SRI")]
    [EndpointDescription("Genera el comprobante electrónico codDoc 04 (v1.1.0), lo firma digitalmente con el certificado del Emisor y lo envía al servicio web de recepción del SRI.")]
    public static async Task<Ok<EmitirNotaCreditoResponseDto>> EmitirNotaCredito(ISender sender, EmitirNotaCreditoCommand command)
    {
        var response = await sender.Send(command);

        return TypedResults.Ok(response);
    }

    [EndpointSummary("Consultar Autorización de Nota de Crédito en el SRI")]
    [EndpointDescription("Consulta el estado de autorización de una nota de crédito en los servidores del SRI a partir de su clave de acceso.")]
    public static async Task<Ok<SriAutorizacionResponseDto>> ConsultarAutorizacionNotaCredito(ISender sender, string claveAcceso)
    {
        var response = await sender.Send(new ConsultarAutorizacionNotaCreditoQuery(claveAcceso));

        return TypedResults.Ok(response);
    }

    [EndpointSummary("Descargar RIDE PDF de Nota de Crédito")]
    [EndpointDescription("Genera la representación impresa (RIDE) en formato PDF de la nota de crédito especificada con disposición inline.")]
    public static async Task<IResult> DescargarPdfNotaCredito(ISender sender, HttpContext httpContext, int id)
    {
        var result = await sender.Send(new GetNotaCreditoPdfQuery(id));

        httpContext.Response.Headers.ContentDisposition = $"inline; filename=\"{result.FileName}\"";
        return Results.File(result.Content, "application/pdf");
    }

    [EndpointSummary("Descargar Comprobante XML de Nota de Crédito")]
    [EndpointDescription("Obtiene el archivo XML oficial (autorizado o firmado) de la nota de crédito electrónica para su descarga o integración contable.")]
    public static async Task<IResult> DescargarXmlNotaCredito(ISender sender, HttpContext httpContext, int id, bool? raw)
    {
        var result = await sender.Send(new GetNotaCreditoXmlQuery(id, raw ?? false));

        httpContext.Response.Headers.ContentDisposition = $"attachment; filename=\"{result.FileName}\"";
        return Results.File(result.Content, result.ContentType);
    }

    [EndpointSummary("Enviar o reenviar nota de crédito por correo")]
    [EndpointDescription("Envía la representación impresa (RIDE PDF) y el archivo XML firmado al correo del comprador o a un correo alternativo especificado.")]
    public static async Task<IResult> EnviarCorreoNotaCredito(ISender sender, int id, [FromQuery] string? emailDestino)
    {
        var result = await sender.Send(new EnviarNotaCreditoEmailCommand { NotaCreditoId = id, EmailDestino = emailDestino });
        return TypedResults.Ok(new { Mensaje = "Comprobante procesado para envío por correo.", Enviado = result });
    }
}

