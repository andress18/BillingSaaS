namespace BillingSaaS.Domain.Entities;

public class Comprador : BaseAuditableEntity
{
    public string TipoIdentificacion { get; private init; } = null!;
    public string Identificacion { get; private init; } = null!; 
    public string RazonSocial { get; private set; } = null!; 
    public string? Direccion { get; private set; } 
    public string? CorreoElectronico { get; private set; }

    private Comprador() { }

    public static Comprador Crear(string tipoIdentificacion, string identificacion, string razonSocial, string? direccion = null, string? correoElectronico = null)
    {
        if (string.IsNullOrWhiteSpace(tipoIdentificacion) || tipoIdentificacion.Length > 2)
            throw new ArgumentException("El tipo de identificación es obligatorio y debe tener máximo 2 caracteres."); //[cite: 1]

        if (string.IsNullOrWhiteSpace(identificacion) || identificacion.Length > 20)
            throw new ArgumentException("La identificación es obligatoria y debe tener máximo 20 caracteres."); //[cite: 1]

        if (string.IsNullOrWhiteSpace(razonSocial) || razonSocial.Length > 300)
            throw new ArgumentException("La razón social es obligatoria y debe tener máximo 300 caracteres."); //[cite: 1]

        if (direccion is { Length: > 300 })
            throw new ArgumentException("La dirección no puede exceder los 300 caracteres."); //[cite: 1]

        return new Comprador
        {
            TipoIdentificacion = tipoIdentificacion,
            Identificacion = identificacion,
            RazonSocial = razonSocial,
            Direccion = direccion,
            CorreoElectronico = correoElectronico
        };
    }

    public bool EsConsumidorFinal()
    {
        return TipoIdentificacion == "07" && Identificacion == "9999999999999"; //[cite: 1]
    }
}
