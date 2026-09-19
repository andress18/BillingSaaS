using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using BillingSaaS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Suscripciones.Commands.RechazarRenovacion;

public record RechazarRenovacionCommand(int SolicitudId, string MotivoRechazo) : IRequest<Unit>;

public class RechazarRenovacionCommandHandler : IRequestHandler<RechazarRenovacionCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public RechazarRenovacionCommandHandler(
        IApplicationDbContext context,
        TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<Unit> Handle(RechazarRenovacionCommand request, CancellationToken cancellationToken)
    {
        var solicitud = await _context.SolicitudesRenovacion
            .FirstOrDefaultAsync(s => s.Id == request.SolicitudId, cancellationToken);

        Guard.Against.NotFound(request.SolicitudId, solicitud);

        var ahoraUtc = _timeProvider.GetUtcNow().UtcDateTime;
        solicitud.Rechazar(request.MotivoRechazo, ahoraUtc);

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

