using BillingSaaS.Application.Common.Models;
using BillingSaaS.Application.Common.Models.Sri;
using BillingSaaS.Application.NotasDebito.Commands.EmitirNotaDebito;
using BillingSaaS.Application.NotasDebito.Commands.EnviarNotaDebitoEmail;
using BillingSaaS.Application.NotasDebito.Queries.ConsultarAutorizacionNotaDebito;
using BillingSaaS.Application.NotasDebito.Queries.GetNotaDebitoPdf;
using BillingSaaS.Application.NotasDebito.Queries.GetNotaDebitoXml;
using BillingSaaS.Application.NotasDebito.Queries.GetNotasDebito;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BillingSaaS.Web.Endpoints;

public class NotasDebito : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();

        groupBuilder.MapPost(EmitirNotaDebito);
        groupBuilder.MapGet(GetNotasDebito);
        groupBuilder.MapGet(ConsultarAutorizacion, "{claveAcceso}/autorizacion");
        groupBuilder.MapGet(DescargarPdf, "{id:int}/pdf");
        groupBuilder.MapGet(DescargarXml, "{id:int}/xml");
        groupBuilder.MapPost(EnviarCorreo, "{id:int}/enviar-correo");
    }

    [EndpointSummary("Listar notas de débito electrónicas")]
    [EndpointDescription("Obtiene un listado paginado y filtrado de las notas de débito emitidas por emisor, estado, comprador o rango de fechas.")]
    public static async Task<Ok<PaginatedList<NotaDebitoBriefDto>>> GetNotasDebito(ISender sender, [AsParameters] GetNotasDebitoQuery query)
    {
        var response = await sender.Send(query);

        return TypedResults.Ok(response);
    }

    [EndpointSummary("Emitir y firmar Nota de Débito Electrónica SRI")]
    [EndpointDescription("Genera el comprobante electrónico codDoc 05 (v1.0.0), lo firma digitalmente con el certificado del Emisor y lo envía al servicio web de recepción del SRI.")]
    public static async Task<Ok<EmitirNotaDebitoResponseDto>> EmitirNotaDebito(ISender sender, EmitirNotaDebitoCommand command)
    {
        var response = await sender.Send(command);

        return TypedResults.Ok(response);
    }

    [EndpointSummary("Consultar Autorización de Nota de Débito en el SRI")]
    [EndpointDescription("Consulta el estado de autorización de una nota de débito en los servidores del SRI a partir de su clave de acceso.")]
    public static async Task<Ok<SriAutorizacionResponseDto>> ConsultarAutorizacion(ISender sender, string claveAcceso)
    {
        var response = await sender.Send(new ConsultarAutorizacionNotaDebitoQuery(claveAcceso));

        return TypedResults.Ok(response);
    }

    [EndpointSummary("Descargar RIDE PDF de Nota de Débito")]
    [EndpointDescription("Genera la representación impresa (RIDE) en formato PDF de la nota de débito especificada con disposición inline.")]
    public static async Task<IResult> DescargarPdf(ISender sender, HttpContext httpContext, int id)
    {
        var result = await sender.Send(new GetNotaDebitoPdfQuery(id));

        httpContext.Response.Headers.ContentDisposition = $"inline; filename=\"{result.FileName}\"";
        return Results.File(result.Content, "application/pdf");
    }

    [EndpointSummary("Descargar Comprobante XML de Nota de Débito")]
    [EndpointDescription("Obtiene el archivo XML oficial (autorizado o firmado) de la nota de débito electrónica para su descarga o integración contable.")]
    public static async Task<IResult> DescargarXml(ISender sender, HttpContext httpContext, int id, bool? raw)
    {
        var result = await sender.Send(new GetNotaDebitoXmlQuery(id, raw ?? false));

        httpContext.Response.Headers.ContentDisposition = $"attachment; filename=\"{result.FileName}\"";
        return Results.File(result.Content, result.ContentType);
    }

    [EndpointSummary("Enviar o reenviar nota de débito por correo")]
    [EndpointDescription("Envía la representación impresa (RIDE PDF) y el archivo XML firmado al correo del comprador o a un correo alternativo especificado.")]
    public static async Task<IResult> EnviarCorreo(ISender sender, int id, [FromQuery] string? emailDestino)
    {
        var result = await sender.Send(new EnviarNotaDebitoEmailCommand { NotaDebitoId = id, EmailDestino = emailDestino });
        return TypedResults.Ok(new { Mensaje = "Comprobante procesado para envío por correo.", Enviado = result });
    }
}

