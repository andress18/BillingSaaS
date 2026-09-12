namespace BillingSaaS.Application.Common.Models.Sri;

public class SriRecepcionResponseDto
{
    public string Estado { get; set; } = string.Empty; // RECIBIDA o DEVUELTA
    public bool EsRecibida => string.Equals(Estado, "RECIBIDA", StringComparison.OrdinalIgnoreCase);
    public List<SriComprobanteRecepcionDto> Comprobantes { get; set; } = new();
}

public class SriComprobanteRecepcionDto
{
    public string ClaveAcceso { get; set; } = string.Empty;
    public List<SriMensajeDto> Mensajes { get; set; } = new();
}

public class SriMensajeDto
{
    public string Identificador { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public string? InformacionAdicional { get; set; }
    public string Tipo { get; set; } = string.Empty; // ERROR, ADVERTENCIA, INFORMACION
}

