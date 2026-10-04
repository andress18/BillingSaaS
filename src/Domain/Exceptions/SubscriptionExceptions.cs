using System;

namespace BillingSaaS.Domain.Exceptions;

public class SubscriptionRequiredException : Exception
{
    public SubscriptionRequiredException(Guid tenantId)
        : base($"El tenant '{tenantId}' no tiene una suscripción activa configurada en la plataforma.") { }
}

public class TenantPaymentRequiredException : Exception
{
    public Guid TenantId { get; }
    public string NombreOrganizacion { get; }

    public TenantPaymentRequiredException(Guid tenantId, string nombreOrganizacion)
        : base($"La cuenta '{nombreOrganizacion}' se encuentra en estado 'Pendiente de Pago'. Se requiere la confirmación del pago de la suscripción/comisión para habilitar la emisión de comprobantes electrónicos.")
    {
        TenantId = tenantId;
        NombreOrganizacion = nombreOrganizacion;
    }
}

public class SubscriptionExpiredException : Exception
{
    public DateTime FechaVencimiento { get; }

    public SubscriptionExpiredException(DateTime fechaVencimiento)
        : base($"La suscripción expiró el {fechaVencimiento:dd/MM/yyyy}. Es necesario renovar el plan para continuar emitiendo comprobantes.")
    {
        FechaVencimiento = fechaVencimiento;
    }
}

public class SubscriptionLimitExceededException : Exception
{
    public string PlanNombre { get; }
    public int LimiteDocumentos { get; }
    public int Emitidos { get; }

    public SubscriptionLimitExceededException(string planNombre, int limite, int emitidos)
        : base($"Has alcanzado el límite de {limite} documentos de tu plan '{planNombre}' (emitidos: {emitidos}). Actualiza tu plan para continuar facturando.")
    {
        PlanNombre = planNombre;
        LimiteDocumentos = limite;
        Emitidos = emitidos;
    }
}

public class DocumentTypeNotAllowedException : Exception
{
    public string TipoComprobante { get; }
    public string PlanNombre { get; }

    public DocumentTypeNotAllowedException(string tipoComprobante, string planNombre)
        : base($"El tipo de comprobante '{tipoComprobante}' no está incluido en el plan '{planNombre}'.")
    {
        TipoComprobante = tipoComprobante;
        PlanNombre = planNombre;
    }
}

public class EstablishmentLimitExceededException : Exception
{
    public int MaxPermitido { get; }
    public int Solicitado { get; }

    public EstablishmentLimitExceededException(int maxPermitido, int solicitado)
        : base($"El plan contratado permite hasta {maxPermitido} establecimiento(s). Se intentó utilizar el establecimiento #{solicitado}.")
    {
        MaxPermitido = maxPermitido;
        Solicitado = solicitado;
    }
}

