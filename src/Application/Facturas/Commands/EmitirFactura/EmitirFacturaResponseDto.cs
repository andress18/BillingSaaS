namespace BillingSaaS.Application.Facturas.Commands.EmitirFactura;

public class EmitirFacturaResponseDto
{
    public int FacturaId { get; set; }
    public string ClaveAcceso { get; set; } = string.Empty;
    public string Secuencial { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty; // RECIBIDA, DEVUELTA, etc.
    public bool EsRecibida { get; set; }
    public string? MensajeDevolucion { get; set; }
    public int ProximoSecuencial { get; set; }
    public string ProximoSecuencialNumero { get; set; } = string.Empty;
}

