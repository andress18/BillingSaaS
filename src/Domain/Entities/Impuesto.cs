namespace BillingSaaS.Domain.Entities;

public class Impuesto
{
    public string Codigo { get; private set; } = null!;
    public string CodigoPorcentaje { get; private set; } = null!; 
    public decimal Tarifa { get; private set; } 
    public decimal BaseImponible { get; private set; } 
    public decimal Valor { get; private set; } 

    private Impuesto() { }

    public static Impuesto Crear(string codigo, string codigoPorcentaje, decimal tarifa, decimal baseImponible)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            throw new ArgumentException("El código del impuesto es obligatorio (ej. 2 para IVA)."); //[cite: 1]

        if (string.IsNullOrWhiteSpace(codigoPorcentaje))
            throw new ArgumentException("El código de porcentaje es obligatorio."); //[cite: 1]

        var impuesto = new Impuesto
        {
            Codigo = codigo,
            CodigoPorcentaje = codigoPorcentaje,
            Tarifa = tarifa,
            BaseImponible = baseImponible,
        
            // El valor del impuesto se calcula basado en la base imponible y la tarifa
            Valor = Math.Round(baseImponible * (tarifa / 100), 2)
        };

        return impuesto;
    }
}
