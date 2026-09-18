using System;

namespace BillingSaaS.Domain.Entities;

public class TenantSubscription : BaseAuditableEntity
{
    public Guid TenantId { get; private set; }
    public int PlanId { get; private set; }
    public Plan Plan { get; private set; } = null!;

    public DateTime FechaInicio { get; private set; }
    public DateTime FechaVencimiento { get; private set; }
    public string Frecuencia { get; private set; } = "MENSUAL"; // "MENSUAL", "ANUAL"
    public string Estado { get; private set; } = "ACTIVO";      // "ACTIVO", "PERIODO_GRACIA", "VENCIDO", "CANCELADO"
    public int DiasGracia { get; private set; } = 5;

    private TenantSubscription() { }

    public static TenantSubscription Crear(
        Guid tenantId,
        int planId,
        DateTime fechaInicio,
        DateTime fechaVencimiento,
        string frecuencia = "MENSUAL",
        int diasGracia = 5)
    {
        return tenantId == Guid.Empty
            ? throw new ArgumentException("El TenantId es obligatorio.", nameof(tenantId))
            : planId <= 0
            ? throw new ArgumentException("El PlanId debe ser válido.", nameof(planId))
            : fechaVencimiento < fechaInicio
            ? throw new ArgumentException("La fecha de vencimiento no puede ser anterior a la fecha de inicio.")
            : new TenantSubscription
        {
            TenantId = tenantId,
            PlanId = planId,
            FechaInicio = fechaInicio,
            FechaVencimiento = fechaVencimiento,
            Frecuencia = frecuencia.Trim().ToUpperInvariant(),
            Estado = "ACTIVO",
            DiasGracia = Math.Max(0, diasGracia)
        };
    }

    public bool EstaVigente(DateTime fechaUtc)
    {
        return Estado != "CANCELADO" && fechaUtc <= FechaVencimiento.AddDays(DiasGracia);
    }

    public bool EstaEnPeriodoGracia(DateTime fechaUtc)
    {
        return Estado != "CANCELADO" && fechaUtc > FechaVencimiento && fechaUtc <= FechaVencimiento.AddDays(DiasGracia);
    }

    public int ObtenerDiasRestantes(DateTime fechaUtc)
    {
        var diff = (FechaVencimiento.Date - fechaUtc.Date).TotalDays;
        return Math.Max(0, (int)diff);
    }

    public DateTime ObtenerInicioCicloActual(DateTime fechaUtc)
    {
        if (Frecuencia == "ANUAL")
        {
            // Si es anual, el ciclo inició en FechaInicio o el aniversario más reciente
            var años = fechaUtc.Year - FechaInicio.Year;
            var inicioEsteAño = FechaInicio.AddYears(años);
            return inicioEsteAño <= fechaUtc ? inicioEsteAño : FechaInicio.AddYears(años - 1);
        }

        // Si es mensual, el ciclo inicia en el mismo día del mes correspondiente a FechaInicio
        int diaCiclo = Math.Min(FechaInicio.Day, 28);
        var inicioMes = new DateTime(fechaUtc.Year, fechaUtc.Month, diaCiclo, 0, 0, 0, DateTimeKind.Utc);
        if (inicioMes > fechaUtc)
        {
            inicioMes = inicioMes.AddMonths(-1);
        }
        return inicioMes;
    }

    public void Renovar(DateTime nuevaFechaVencimiento)
    {
        if (nuevaFechaVencimiento <= FechaVencimiento)
            throw new ArgumentException("La nueva fecha de vencimiento debe ser posterior a la actual.");

        FechaVencimiento = nuevaFechaVencimiento;
        Estado = "ACTIVO";
    }

    public void CambiarPlan(int nuevoPlanId, string nuevaFrecuencia, DateTime nuevaFechaVencimiento)
    {
        if (nuevoPlanId <= 0)
            throw new ArgumentException("El nuevo PlanId debe ser válido.", nameof(nuevoPlanId));

        PlanId = nuevoPlanId;
        Frecuencia = nuevaFrecuencia.Trim().ToUpperInvariant();
        FechaVencimiento = nuevaFechaVencimiento;
        Estado = "ACTIVO";
    }

    public void Cancelar()
    {
        Estado = "CANCELADO";
    }
}

