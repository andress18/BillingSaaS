namespace BillingSaaS.Domain.Entities;

using System;

public class ImpuestoNotaDebito : BaseAuditableEntity
{
    public int NotaDebitoId { get; private set; }
    public string Codigo { get; private set; } = null!;           // 2: IVA
    public string CodigoPorcentaje { get; private set; } = null!; // 0: 0%, 4: 15%, 2: 12%, etc.
    public decimal Tarifa { get; private set; }                   // 15.00, 0.00
    public decimal BaseImponible { get; private set; }
    public decimal Valor { get; private set; }

    private ImpuestoNotaDebito() { }

    public static ImpuestoNotaDebito Crear(string codigo, string codigoPorcentaje, decimal tarifa, decimal baseImponible)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            throw new ArgumentException("El código de impuesto es obligatorio.", nameof(codigo));

        if (string.IsNullOrWhiteSpace(codigoPorcentaje))
            throw new ArgumentException("El código de porcentaje es obligatorio.", nameof(codigoPorcentaje));

        if (baseImponible < 0)
            throw new ArgumentException("La base imponible no puede ser negativa.", nameof(baseImponible));

        var valorCalculado = decimal.Round(baseImponible * (tarifa / 100m), 2, MidpointRounding.AwayFromZero);

        return new ImpuestoNotaDebito
        {
            Codigo = codigo,
            CodigoPorcentaje = codigoPorcentaje,
            Tarifa = tarifa,
            BaseImponible = decimal.Round(baseImponible, 2, MidpointRounding.AwayFromZero),
            Valor = valorCalculado
        };
    }

    public static ImpuestoNotaDebito CrearConValor(string codigo, string codigoPorcentaje, decimal tarifa, decimal baseImponible, decimal valor)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            throw new ArgumentException("El código de impuesto es obligatorio.", nameof(codigo));

        if (string.IsNullOrWhiteSpace(codigoPorcentaje))
            throw new ArgumentException("El código de porcentaje es obligatorio.", nameof(codigoPorcentaje));

        return new ImpuestoNotaDebito
        {
            Codigo = codigo,
            CodigoPorcentaje = codigoPorcentaje,
            Tarifa = tarifa,
            BaseImponible = decimal.Round(baseImponible, 2, MidpointRounding.AwayFromZero),
            Valor = decimal.Round(valor, 2, MidpointRounding.AwayFromZero)
        };
    }
}

