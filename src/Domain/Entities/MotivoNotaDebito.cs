namespace BillingSaaS.Domain.Entities;

using System;

public class MotivoNotaDebito : BaseAuditableEntity
{
    public int NotaDebitoId { get; private set; }
    public string Razon { get; private set; } = null!;
    public decimal Valor { get; private set; }

    private MotivoNotaDebito() { }

    public static MotivoNotaDebito Crear(string razon, decimal valor)
    {
        if (string.IsNullOrWhiteSpace(razon) || razon.Length > 300)
            throw new ArgumentException("La razón del motivo es obligatoria y no puede superar los 300 caracteres.", nameof(razon));

        if (valor <= 0)
            throw new ArgumentException("El valor del motivo debe ser mayor a cero.", nameof(valor));

        return new MotivoNotaDebito
        {
            Razon = razon.Trim(),
            Valor = decimal.Round(valor, 2, MidpointRounding.AwayFromZero)
        };
    }
}

