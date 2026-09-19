using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Application.Suscripciones.Commands.SolicitarRenovacion;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Suscripciones.Queries.GetSolicitudesRenovacion;

public record GetSolicitudesRenovacionQuery(string? Estado = null) : IRequest<List<SolicitudRenovacionDto>>;

public class GetSolicitudesRenovacionQueryHandler : IRequestHandler<GetSolicitudesRenovacionQuery, List<SolicitudRenovacionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSolicitudesRenovacionQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SolicitudRenovacionDto>> Handle(GetSolicitudesRenovacionQuery request, CancellationToken cancellationToken)
    {
        var query = _context.SolicitudesRenovacion
            .AsNoTracking()
            .Include(s => s.Plan)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Estado))
        {
            var estadoFiltro = request.Estado.Trim().ToUpperInvariant();
            query = query.Where(s => s.Estado == estadoFiltro);
        }

        return await query
            .OrderByDescending(s => s.FechaSolicitud)
            .Select(s => new SolicitudRenovacionDto
            {
                Id = s.Id,
                TenantId = s.TenantId,
                PlanId = s.PlanId,
                PlanNombre = s.Plan.Nombre,
                Frecuencia = s.Frecuencia,
                Monto = s.Monto,
                MetodoPago = s.MetodoPago,
                BancoOrigen = s.BancoOrigen,
                NumeroComprobante = s.NumeroComprobante,
                Observaciones = s.Observaciones,
                Estado = s.Estado,
                FechaSolicitud = s.FechaSolicitud,
                Mensaje = s.Estado == "PENDIENTE" ? "Pendiente de validación bancaria" : s.Estado
            })
            .ToListAsync(cancellationToken);
    }
}

