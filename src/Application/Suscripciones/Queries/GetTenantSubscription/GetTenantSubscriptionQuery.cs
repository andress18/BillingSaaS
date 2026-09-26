using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Suscripciones.Queries.GetTenantSubscription;

public record TenantSubscriptionDto
{
    public string PlanNombre { get; init; } = string.Empty;
    public string PlanCodigo { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public string Frecuencia { get; init; } = string.Empty;
    public DateTime FechaInicio { get; init; }
    public DateTime FechaVencimiento { get; init; }
    public int DiasRestantes { get; init; }
    public bool EnPeriodoGracia { get; init; }

    // Consumo del periodo actual
    public bool EsIlimitado { get; init; }
    public int DocumentosEmitidos { get; init; }
    public int FacturasEmitidas { get; init; }
    public int NotasCreditoEmitidas { get; init; }
    public int NotasDebitoEmitidas { get; init; }
    public int? DocumentosMaximos { get; init; }
    public int? DocumentosDisponibles { get; init; }
    public decimal PorcentajeUso { get; init; }

    // Establecimientos
    public int MaxEstablecimientos { get; init; }
    public int EstablecimientosRegistrados { get; init; }

    // Tipos de documentos
    public bool PermiteGuiasRemision { get; init; }
    public bool PermiteLiquidaciones { get; init; }
    public bool PermiteNotasDebito { get; init; }
    public bool PermiteNotasCredito { get; init; }
    public bool PermiteFacturas { get; init; }

    // Solicitud de renovación pendiente de validación manual
    public bool TieneSolicitudPendiente { get; init; }
    public int? SolicitudPendienteId { get; init; }
    public string? NumeroComprobantePendiente { get; init; }
    public DateTime? FechaSolicitudPendiente { get; init; }
}

public record GetTenantSubscriptionQuery : IRequest<TenantSubscriptionDto>;

public class GetTenantSubscriptionQueryHandler : IRequestHandler<GetTenantSubscriptionQuery, TenantSubscriptionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly TimeProvider _timeProvider;

    public GetTenantSubscriptionQueryHandler(
        IApplicationDbContext context,
        IUser user,
        TimeProvider timeProvider)
    {
        _context = context;
        _user = user;
        _timeProvider = timeProvider;
    }

    public async Task<TenantSubscriptionDto> Handle(GetTenantSubscriptionQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _user.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("El usuario no tiene un Tenant asignado.");
        }

        var suscripcion = await _context.Suscripciones
            .AsNoTracking()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

        if (suscripcion == null)
        {
            throw new SubscriptionRequiredException(tenantId);
        }

        var ahoraUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var inicioCiclo = suscripcion.ObtenerInicioCicloActual(ahoraUtc);

        var emitidos = await _context.Facturas
            .CountAsync(f => f.TenantId == tenantId && f.FechaEmision >= inicioCiclo && f.Estado != "DEVUELTA", cancellationToken);

        var notasCreditoEmitidas = await _context.NotasCredito
            .CountAsync(nc => nc.TenantId == tenantId && nc.FechaEmision >= inicioCiclo && nc.Estado != "DEVUELTA", cancellationToken);

        var notasDebitoEmitidas = await _context.NotasDebito
            .CountAsync(nd => nd.TenantId == tenantId && nd.FechaEmision >= inicioCiclo && nd.Estado != "DEVUELTA", cancellationToken);

        var establecimientosRegistrados = await _context.Emisores
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.CodigoEstablecimiento)
            .Distinct()
            .CountAsync(cancellationToken);

        var solicitudPendiente = await _context.SolicitudesRenovacion
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.Estado == "PENDIENTE")
            .OrderByDescending(s => s.FechaSolicitud)
            .FirstOrDefaultAsync(cancellationToken);

        var plan = suscripcion.Plan;
        bool esIlimitado = plan.EsIlimitado(suscripcion.Frecuencia);
        int? limite = plan.ObtenerLimiteDocumentos(suscripcion.Frecuencia);
        int? disponibles = esIlimitado ? null : Math.Max(0, limite!.Value - emitidos);
        decimal porcentajeUso = (esIlimitado || limite == null || limite.Value == 0)
            ? 0m
            : Math.Round(Math.Min(100m, (decimal)emitidos / limite.Value * 100m), 1);

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
            DocumentosEmitidos = emitidos,
            FacturasEmitidas = emitidos,
            NotasCreditoEmitidas = notasCreditoEmitidas,
            NotasDebitoEmitidas = notasDebitoEmitidas,
            DocumentosMaximos = limite,
            DocumentosDisponibles = disponibles,
            PorcentajeUso = porcentajeUso,

            MaxEstablecimientos = plan.MaxEstablecimientos,
            EstablecimientosRegistrados = Math.Max(1, establecimientosRegistrados),

            PermiteFacturas = plan.PermiteTipoDocumento("01"),
            PermiteLiquidaciones = plan.PermiteTipoDocumento("03"),
            PermiteNotasCredito = plan.PermiteTipoDocumento("04"),
            PermiteNotasDebito = plan.PermiteTipoDocumento("05"),
            PermiteGuiasRemision = plan.PermiteTipoDocumento("06"),

            TieneSolicitudPendiente = solicitudPendiente != null,
            SolicitudPendienteId = solicitudPendiente?.Id,
            NumeroComprobantePendiente = solicitudPendiente?.NumeroComprobante,
            FechaSolicitudPendiente = solicitudPendiente?.FechaSolicitud
        };
    }
}

