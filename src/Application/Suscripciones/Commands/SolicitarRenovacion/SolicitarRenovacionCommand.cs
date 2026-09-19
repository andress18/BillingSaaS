using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Suscripciones.Commands.SolicitarRenovacion;

public record SolicitudRenovacionDto
{
    public int Id { get; init; }
    public Guid TenantId { get; init; }
    public int PlanId { get; init; }
    public string PlanNombre { get; init; } = string.Empty;
    public string Frecuencia { get; init; } = string.Empty;
    public decimal Monto { get; init; }
    public string MetodoPago { get; init; } = string.Empty;
    public string? BancoOrigen { get; init; }
    public string NumeroComprobante { get; init; } = string.Empty;
    public string? Observaciones { get; init; }
    public string Estado { get; init; } = string.Empty;
    public DateTime FechaSolicitud { get; init; }
    public string Mensaje { get; init; } = string.Empty;
}

public record SolicitarRenovacionCommand : IRequest<SolicitudRenovacionDto>
{
    public int PlanId { get; init; }
    public string Frecuencia { get; init; } = "MENSUAL"; // "MENSUAL" | "ANUAL"
    public string NumeroComprobante { get; init; } = string.Empty;
    public string? BancoOrigen { get; init; }
    public string? MetodoPago { get; init; } = "TRANSFERENCIA";
    public string? Observaciones { get; init; }
}

public class SolicitarRenovacionCommandHandler : IRequestHandler<SolicitarRenovacionCommand, SolicitudRenovacionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly TimeProvider _timeProvider;

    public SolicitarRenovacionCommandHandler(
        IApplicationDbContext context,
        IUser user,
        TimeProvider timeProvider)
    {
        _context = context;
        _user = user;
        _timeProvider = timeProvider;
    }

    public async Task<SolicitudRenovacionDto> Handle(SolicitarRenovacionCommand request, CancellationToken cancellationToken)
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

        if (string.IsNullOrWhiteSpace(request.NumeroComprobante))
        {
            throw new ArgumentException("El número de comprobante o referencia es obligatorio.", nameof(request.NumeroComprobante));
        }

        var ahoraUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var monto = frecuencia == "ANUAL" ? plan.PrecioAnual : plan.PrecioMensual;

        // Verificar si ya existe una solicitud pendiente
        var solicitudExistente = await _context.SolicitudesRenovacion
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Estado == "PENDIENTE", cancellationToken);

        SolicitudRenovacion solicitud;
        if (solicitudExistente != null)
        {
            // Rechazar la anterior o reemplazarla con la nueva
            solicitudExistente.Rechazar("Reemplazada por nueva solicitud del cliente", ahoraUtc);
        }

        solicitud = SolicitudRenovacion.Crear(
            tenantId,
            plan.Id,
            frecuencia,
            monto,
            request.NumeroComprobante,
            ahoraUtc,
            request.MetodoPago ?? "TRANSFERENCIA",
            request.BancoOrigen,
            request.Observaciones
        );

        _context.SolicitudesRenovacion.Add(solicitud);
        await _context.SaveChangesAsync(cancellationToken);

        return new SolicitudRenovacionDto
        {
            Id = solicitud.Id,
            TenantId = solicitud.TenantId,
            PlanId = plan.Id,
            PlanNombre = plan.Nombre,
            Frecuencia = solicitud.Frecuencia,
            Monto = solicitud.Monto,
            MetodoPago = solicitud.MetodoPago,
            BancoOrigen = solicitud.BancoOrigen,
            NumeroComprobante = solicitud.NumeroComprobante,
            Observaciones = solicitud.Observaciones,
            Estado = solicitud.Estado,
            FechaSolicitud = solicitud.FechaSolicitud,
            Mensaje = "Solicitud de renovación registrada exitosamente. Será validada por el administrador."
        };
    }
}

