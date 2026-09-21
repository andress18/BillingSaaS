namespace BillingSaaS.Application.NotasDebito.Commands.EmitirNotaDebito;

public class EmitirNotaDebitoResponseDto
{
    public int NotaDebitoId { get; set; }
    public string ClaveAcceso { get; set; } = string.Empty;
    public string Secuencial { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public bool EsRecibida { get; set; }
    public string? MensajeDevolucion { get; set; }
}

