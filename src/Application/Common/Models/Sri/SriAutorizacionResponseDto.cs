namespace BillingSaaS.Application.Common.Models.Sri;

public class SriAutorizacionResponseDto
{
    public string ClaveAccesoConsultada { get; set; } = string.Empty;
    public string NumeroComprobantes { get; set; } = string.Empty;
    public List<SriAutorizacionDto> Autorizaciones { get; set; } = new();

    public SriAutorizacionDto? PrimeraAutorizacion => Autorizaciones.FirstOrDefault();
    public bool EstaAutorizado => Autorizaciones.Any(a => string.Equals(a.Estado, "AUTORIZADO", StringComparison.OrdinalIgnoreCase));
}

public class SriAutorizacionDto
{
    public string Estado { get; set; } = string.Empty; // AUTORIZADO, NO AUTORIZADO, EN PROCESO
    public string? NumeroAutorizacion { get; set; }
    public DateTime? FechaAutorizacion { get; set; }
    public string? Ambiente { get; set; }
    public string? ComprobanteXml { get; set; }
    public List<SriMensajeDto> Mensajes { get; set; } = new();
}

