namespace BillingSaaS.Domain.Entities;

using System;

public class PagoNotaDebito : BaseAuditableEntity
{
    public int NotaDebitoId { get; private set; }
    public string FormaPago { get; private set; } = "01"; // 01: Sin utilización del sistema financiero, 20: Otros con utilización
    public decimal Total { get; private set; }
    public decimal? Plazo { get; private set; }
    public string? UnidadTiempo { get; private set; }     // dias, meses, anios

    private PagoNotaDebito() { }

    public static PagoNotaDebito Crear(string formaPago, decimal total, decimal? plazo = null, string? unidadTiempo = null)
    {
        if (string.IsNullOrWhiteSpace(formaPago))
            throw new ArgumentException("La forma de pago es obligatoria.", nameof(formaPago));

        if (total <= 0)
            throw new ArgumentException("El total del pago debe ser mayor a cero.", nameof(total));

        return new PagoNotaDebito
        {
            FormaPago = formaPago,
            Total = decimal.Round(total, 2, MidpointRounding.AwayFromZero),
            Plazo = plazo,
            UnidadTiempo = unidadTiempo
        };
    }
}

