using System;
using System.Threading;
using System.Threading.Tasks;
using BillingSaaS.Application.Common.Exceptions;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Application.Suscripciones.Queries.GetTenantSubscription;
using BillingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Suscripciones.Commands.RenovarSuscripcion;

public record RenovarSuscripcionCommand : IRequest<TenantSubscriptionDto>
{
    public int PlanId { get; init; }
    public string Frecuencia { get; init; } = "MENSUAL"; // "MENSUAL" | "ANUAL"
    public string? MetodoPago { get; init; } = "DIRECTO"; // "TRANSFERENCIA", "TARJETA", "DIRECTO"
    public string? ReferenciaPago { get; init; }
}

public class RenovarSuscripcionCommandHandler : IRequestHandler<RenovarSuscripcionCommand, TenantSubscriptionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly ISender _sender;
    private readonly TimeProvider _timeProvider;

    public RenovarSuscripcionCommandHandler(
        IApplicationDbContext context,
        IUser user,
        ISender sender,
        TimeProvider timeProvider)
    {
        _context = context;
        _user = user;
        _sender = sender;
        _timeProvider = timeProvider;
    }

    public async Task<TenantSubscriptionDto> Handle(RenovarSuscripcionCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _user.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("El usuario no tiene un Tenant asignado.");
        }

        var plan = await _context.Planes
            .FirstOrDefaultAsync(p => p.Id == request.PlanId, cancellationToken);

        Guard.Against.NotFound(request.PlanId, plan);

        if (!plan.Activo)
        {
            throw new InvalidOperationException("El plan seleccionado no se encuentra disponible.");
        }

        var frecuencia = request.Frecuencia.Trim().ToUpperInvariant();
        if (frecuencia is not ("MENSUAL" or "ANUAL"))
        {
            throw new ArgumentException("La frecuencia debe ser 'MENSUAL' o 'ANUAL'.", nameof(request.Frecuencia));
        }

        var ahoraUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var suscripcion = await _context.Suscripciones
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

        if (suscripcion == null)
        {
            // Si el tenant no tenía suscripción previa, crearla inmediatamente
            var fechaFin = frecuencia == "ANUAL" ? ahoraUtc.AddYears(1) : ahoraUtc.AddMonths(1);
            suscripcion = TenantSubscription.Crear(
                tenantId,
                plan.Id,
                ahoraUtc,
                fechaFin,
                frecuencia
            );
            _context.Suscripciones.Add(suscripcion);
        }
        else
        {
            // Si es el mismo plan, extiende la vigencia
            if (suscripcion.PlanId == plan.Id)
            {
                suscripcion.ExtenderVigencia(frecuencia, ahoraUtc);
            }
            else
            {
                // Si es un cambio de plan (Upgrade/Downgrade), reinicia el ciclo con el nuevo plan
                suscripcion.CambiarPlan(plan.Id, frecuencia, ahoraUtc);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Devolver la información actualizada de la suscripción
        return await _sender.Send(new GetTenantSubscriptionQuery(), cancellationToken);
    }
}
