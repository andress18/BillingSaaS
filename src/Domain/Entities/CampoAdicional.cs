namespace BillingSaaS.Domain.Entities;

public class CampoAdicional
{
    public string Nombre { get; private set; } = null!;
    public string Valor { get; private set; } = null!;

    private CampoAdicional() { }

    public static CampoAdicional Crear(string nombre, string valor)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del campo adicional es obligatorio.", nameof(nombre));

        if (string.IsNullOrWhiteSpace(valor))
            throw new ArgumentException("El valor del campo adicional es obligatorio.", nameof(valor));

        return new CampoAdicional
        {
            Nombre = nombre.Trim().Length > 300 ? nombre.Trim()[..300] : nombre.Trim(),
            Valor = valor.Trim().Length > 300 ? valor.Trim()[..300] : valor.Trim()
        };
    }
}
