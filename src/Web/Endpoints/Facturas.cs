using BillingSaaS.Application.Common.Models;
using BillingSaaS.Application.Common.Models.Sri;
using BillingSaaS.Application.Facturas.Commands.EmitirFactura;
using BillingSaaS.Application.Facturas.Queries.ConsultarAutorizacionFactura;
using BillingSaaS.Application.Facturas.Queries.GetFacturaPdf;
using BillingSaaS.Application.Facturas.Queries.GetFacturaXml;
using BillingSaaS.Application.Facturas.Queries.GetFacturas;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BillingSaaS.Web.Endpoints;

public class Facturas : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();

        groupBuilder.MapPost(EmitirFactura);
        groupBuilder.MapGet(GetFacturas);
        groupBuilder.MapGet(ConsultarAutorizacion, "{claveAcceso}/autorizacion");
        groupBuilder.MapGet(DescargarPdf, "{id:int}/pdf");
        groupBuilder.MapGet(DescargarXml, "{id:int}/xml");
    }

    [EndpointSummary("Listar facturas electrónicas")]
    [EndpointDescription("Obtiene un listado paginado y filtrado de las facturas emitidas por emisor, estado, comprador o rango de fechas.")]
    public static async Task<Ok<PaginatedList<FacturaBriefDto>>> GetFacturas(ISender sender, [AsParameters] GetFacturasQuery query)
    {
        var response = await sender.Send(query);

        return TypedResults.Ok(response);
    }

    [EndpointSummary("Emitir y firmar Factura Electrónica SRI")]
    [EndpointDescription("Genera el comprobante electrónico v1.1.0, lo firma digitalmente con el certificado del Emisor y lo envía al servicio web de recepción del SRI.")]
    public static async Task<Ok<EmitirFacturaResponseDto>> EmitirFactura(ISender sender, EmitirFacturaCommand command)
    {
        var response = await sender.Send(command);

        return TypedResults.Ok(response);
    }

    [EndpointSummary("Consultar Autorización en el SRI")]
    [EndpointDescription("Consulta el estado de autorización de un comprobante electrónico en los servidores del SRI a partir de su clave de acceso.")]
    public static async Task<Ok<SriAutorizacionResponseDto>> ConsultarAutorizacion(ISender sender, string claveAcceso)
    {
        var response = await sender.Send(new ConsultarAutorizacionFacturaQuery(claveAcceso));

        return TypedResults.Ok(response);
    }

    [EndpointSummary("Descargar RIDE PDF de Factura")]
    [EndpointDescription("Genera la representación impresa (RIDE) en formato PDF de la factura electrónica especificada con disposición inline.")]
    public static async Task<IResult> DescargarPdf(ISender sender, HttpContext httpContext, int id)
    {
        var result = await sender.Send(new GetFacturaPdfQuery(id));

        httpContext.Response.Headers.ContentDisposition = $"inline; filename=\"{result.FileName}\"";
        return Results.File(result.Content, "application/pdf");
    }

    [EndpointSummary("Descargar Comprobante XML de Factura")]
    [EndpointDescription("Obtiene el archivo XML oficial (autorizado o firmado) de la factura electrónica para su descarga o integración contable.")]
    public static async Task<IResult> DescargarXml(ISender sender, HttpContext httpContext, int id, bool? raw)
    {
        var result = await sender.Send(new GetFacturaXmlQuery(id, raw ?? false));

        httpContext.Response.Headers.ContentDisposition = $"attachment; filename=\"{result.FileName}\"";
        return Results.File(result.Content, result.ContentType);
    }
}

