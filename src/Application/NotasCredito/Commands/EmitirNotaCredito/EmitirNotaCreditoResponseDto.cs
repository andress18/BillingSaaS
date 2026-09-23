namespace BillingSaaS.Application.NotasCredito.Commands.EmitirNotaCredito;

public class EmitirNotaCreditoResponseDto
{
    public int NotaCreditoId { get; init; }
    public string ClaveAcceso { get; init; } = null!;
    public string Secuencial { get; init; } = null!;
    public string Estado { get; init; } = null!;
    public bool EsRecibida { get; init; }
    public string? MensajeDevolucion { get; init; }
}

