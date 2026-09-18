using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillingSaaS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Suscripciones.Queries.GetPlanes;

public record PlanDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public decimal PrecioMensual { get; init; }
    public decimal PrecioAnual { get; init; }
    public int? MaxDocumentosMensuales { get; init; }
    public int? MaxDocumentosAnuales { get; init; }
    public int MaxEstablecimientos { get; init; }
    public bool PermiteFacturas { get; init; }
    public bool PermiteLiquidaciones { get; init; }
    public bool PermiteNotasDebito { get; init; }
    public bool PermiteNotasCredito { get; init; }
    public bool PermiteGuiasRemision { get; init; }
    public bool Destacado { get; init; }
}

public record GetPlanesQuery : IRequest<List<PlanDto>>;

public class GetPlanesQueryHandler : IRequestHandler<GetPlanesQuery, List<PlanDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPlanesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PlanDto>> Handle(GetPlanesQuery request, CancellationToken cancellationToken)
    {
        var planes = await _context.Planes
            .AsNoTracking()
            .Where(p => p.EsPublico && p.Activo)
            .OrderBy(p => p.PrecioMensual)
            .ToListAsync(cancellationToken);

        return planes.Select(p => new PlanDto
        {
            Id = p.Id,
            Codigo = p.Codigo,
            Nombre = p.Nombre,
            Descripcion = p.Descripcion,
            PrecioMensual = p.PrecioMensual,
            PrecioAnual = p.PrecioAnual,
            MaxDocumentosMensuales = p.MaxDocumentosMensuales,
            MaxDocumentosAnuales = p.MaxDocumentosAnuales,
            MaxEstablecimientos = p.MaxEstablecimientos,
            PermiteFacturas = p.PermiteTipoDocumento("01"),
            PermiteLiquidaciones = p.PermiteTipoDocumento("03"),
            PermiteNotasCredito = p.PermiteTipoDocumento("04"),
            PermiteNotasDebito = p.PermiteTipoDocumento("05"),
            PermiteGuiasRemision = p.PermiteTipoDocumento("06"),
            Destacado = p.Codigo == "COMERCIO_PRO"
        }).ToList();
    }
}

