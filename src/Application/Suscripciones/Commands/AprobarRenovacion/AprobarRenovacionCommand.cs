using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Application.Suscripciones.Queries.GetTenantSubscription;
using BillingSaaS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Suscripciones.Commands.AprobarRenovacion;

public record AprobarRenovacionCommand(int SolicitudId) : IRequest<TenantSubscriptionDto>;

public class AprobarRenovacionCommandHandler : IRequestHandler<AprobarRenovacionCommand, TenantSubscriptionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly TimeProvider _timeProvider;

    public AprobarRenovacionCommandHandler(
        IApplicationDbContext context,
        ISender sender,
        TimeProvider timeProvider)
    {
        _context = context;
        _sender = sender;
        _timeProvider = timeProvider;
    }

    public async Task<TenantSubscriptionDto> Handle(AprobarRenovacionCommand request, CancellationToken cancellationToken)
    {
        var solicitud = await _context.SolicitudesRenovacion
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == request.SolicitudId, cancellationToken);

        Guard.Against.NotFound(request.SolicitudId, solicitud);

        var ahoraUtc = _timeProvider.GetUtcNow().UtcDateTime;

        // 1. Aprobar solicitud de renovación
        solicitud.Aprobar(ahoraUtc);

        // 2. Aplicar renovación o cambio de plan a la suscripción del tenant
        var suscripcion = await _context.Suscripciones
            .FirstOrDefaultAsync(s => s.TenantId == solicitud.TenantId, cancellationToken);

        if (suscripcion == null)
        {
            var fechaFin = solicitud.Frecuencia == "ANUAL" ? ahoraUtc.AddYears(1) : ahoraUtc.AddMonths(1);
            suscripcion = TenantSubscription.Crear(
                solicitud.TenantId,
                solicitud.PlanId,
                ahoraUtc,
                fechaFin,
                solicitud.Frecuencia,
                diasGracia: 3
            );
            _context.Suscripciones.Add(suscripcion);
        }
        else
        {
            if (suscripcion.PlanId == solicitud.PlanId)
            {
                // Mismo plan: extiende vigencia
                suscripcion.ExtenderVigencia(solicitud.Frecuencia, ahoraUtc);
            }
            else
            {
                // Cambio de plan / Upgrade: nuevo límite inmediato y reinicio de ciclo
                suscripcion.CambiarPlan(solicitud.PlanId, solicitud.Frecuencia, ahoraUtc);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        var plan = solicitud.Plan;
        bool esIlimitado = plan.EsIlimitado(suscripcion.Frecuencia);
        int? limite = plan.ObtenerLimiteDocumentos(suscripcion.Frecuencia);

        return new TenantSubscriptionDto
        {
            PlanNombre = plan.Nombre,
            PlanCodigo = plan.Codigo,
            Estado = suscripcion.Estado,
            Frecuencia = suscripcion.Frecuencia,
            FechaInicio = suscripcion.FechaInicio,
            FechaVencimiento = suscripcion.FechaVencimiento,
            DiasRestantes = suscripcion.ObtenerDiasRestantes(ahoraUtc),
            EnPeriodoGracia = suscripcion.EstaEnPeriodoGracia(ahoraUtc),
            EsIlimitado = esIlimitado,
            DocumentosEmitidos = 0,
            DocumentosMaximos = limite,
            DocumentosDisponibles = limite,
            PorcentajeUso = 0m,
            MaxEstablecimientos = plan.MaxEstablecimientos,
            EstablecimientosRegistrados = 1,
            PermiteFacturas = plan.PermiteTipoDocumento("01"),
            PermiteLiquidaciones = plan.PermiteTipoDocumento("03"),
            PermiteNotasCredito = plan.PermiteTipoDocumento("04"),
            PermiteNotasDebito = plan.PermiteTipoDocumento("05"),
            PermiteGuiasRemision = plan.PermiteTipoDocumento("06"),
            TieneSolicitudPendiente = false
        };
    }
}

