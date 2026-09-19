using System;

namespace BillingSaaS.Domain.Entities;

public class SolicitudRenovacion : BaseAuditableEntity
{
    public Guid TenantId { get; private set; }
    public int PlanId { get; private set; }
    public Plan Plan { get; private set; } = null!;

    public string Frecuencia { get; private set; } = "MENSUAL"; // "MENSUAL" | "ANUAL"
    public decimal Monto { get; private set; }
    public string MetodoPago { get; private set; } = "TRANSFERENCIA";
    public string? BancoOrigen { get; private set; }
    public string NumeroComprobante { get; private set; } = string.Empty;
    public string? ComprobanteUrl { get; private set; }
    public string? Observaciones { get; private set; }

    public string Estado { get; private set; } = "PENDIENTE"; // "PENDIENTE", "APROBADA", "RECHAZADA"
    public DateTime FechaSolicitud { get; private set; }
    public DateTime? FechaRespuesta { get; private set; }
    public string? MotivoRechazo { get; private set; }

    private SolicitudRenovacion() { }

    public static SolicitudRenovacion Crear(
        Guid tenantId,
        int planId,
        string frecuencia,
        decimal monto,
        string numeroComprobante,
        DateTime fechaSolicitudUtc,
        string metodoPago = "TRANSFERENCIA",
        string? bancoOrigen = null,
        string? observaciones = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El TenantId es obligatorio.", nameof(tenantId));

        if (planId <= 0)
            throw new ArgumentException("El PlanId debe ser válido.", nameof(planId));

        if (string.IsNullOrWhiteSpace(numeroComprobante))
            throw new ArgumentException("El número de comprobante o referencia de transferencia es obligatorio.", nameof(numeroComprobante));

        var freq = frecuencia.Trim().ToUpperInvariant();
        if (freq is not ("MENSUAL" or "ANUAL"))
            throw new ArgumentException("La frecuencia debe ser MENSUAL o ANUAL.", nameof(frecuencia));

        return new SolicitudRenovacion
        {
            TenantId = tenantId,
            PlanId = planId,
            Frecuencia = freq,
            Monto = monto,
            NumeroComprobante = numeroComprobante.Trim(),
            FechaSolicitud = fechaSolicitudUtc,
            MetodoPago = string.IsNullOrWhiteSpace(metodoPago) ? "TRANSFERENCIA" : metodoPago.Trim().ToUpperInvariant(),
            BancoOrigen = bancoOrigen?.Trim(),
            Observaciones = observaciones?.Trim(),
            Estado = "PENDIENTE"
        };
    }

    public void Aprobar(DateTime fechaRespuestaUtc)
    {
        if (Estado != "PENDIENTE")
            throw new InvalidOperationException($"No se puede aprobar una solicitud en estado '{Estado}'.");

        Estado = "APROBADA";
        FechaRespuesta = fechaRespuestaUtc;
    }

    public void Rechazar(string motivo, DateTime fechaRespuestaUtc)
    {
        if (Estado != "PENDIENTE")
            throw new InvalidOperationException($"No se puede rechazar una solicitud en estado '{Estado}'.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("El motivo de rechazo es obligatorio.", nameof(motivo));

        Estado = "RECHAZADA";
        MotivoRechazo = motivo.Trim();
        FechaRespuesta = fechaRespuestaUtc;
    }
}

