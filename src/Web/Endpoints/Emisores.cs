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
        groupBuilder.MapGet(GetRegimenesRimpe, "regimenes-rimpe");
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

    [EndpointSummary("Obtener regímenes tributarios RIMPE del SRI")]
    [EndpointDescription("Devuelve el catálogo de regímenes tributarios (Régimen General, RIMPE Emprendedor y RIMPE Negocio Popular) con sus leyendas y códigos oficiales.")]
    public static Ok<List<RegimenRimpeDto>> GetRegimenesRimpe()
    {
        var result = new List<RegimenRimpeDto>
        {
            new(
                Codigo: Domain.Constants.RegimenRimpeTipos.CodigoGeneral,
                Nombre: "Régimen General",
                LeyendaSri: null,
                Descripcion: "Contribuyentes no sujetos a RIMPE. Facturación general con tarifas de IVA según el producto (15%, 5%, 0%)."
            ),
            new(
                Codigo: Domain.Constants.RegimenRimpeTipos.CodigoEmprendedor,
                Nombre: "Régimen RIMPE - Emprendedor",
                LeyendaSri: Domain.Constants.RegimenRimpeTipos.Emprendedor,
                Descripcion: "Personas naturales y jurídicas con ingresos anuales de hasta $300,000. Emisión de facturas electrónicas con IVA y leyenda oficial 'CONTRIBUYENTE RÉGIMEN RIMPE' en XML y RIDE."
            ),
            new(
                Codigo: Domain.Constants.RegimenRimpeTipos.CodigoNegocioPopular,
                Nombre: "Régimen RIMPE - Negocio Popular",
                LeyendaSri: Domain.Constants.RegimenRimpeTipos.NegocioPopular,
                Descripcion: "Personas naturales con ingresos de hasta $20,000 anuales. Emisión con tarifa 0% de IVA para actividades RIMPE y leyenda obligatoria 'CONTRIBUYENTE NEGOCIO POPULAR - RÉGIMEN RIMPE'."
            )
        };

        return TypedResults.Ok(result);
    }
}

public record RegimenRimpeDto(
    string Codigo,
    string Nombre,
    string? LeyendaSri,
    string Descripcion
);

