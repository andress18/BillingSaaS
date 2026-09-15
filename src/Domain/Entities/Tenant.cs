namespace BillingSaaS.Domain.Entities;

public class Tenant : BaseAuditableEntity
{
    public new Guid Id { get; private set; }
    public string Nombre { get; private set; } = null!;
    public bool Activo { get; private set; } = true;

    /// <summary>
    /// Identificador del Socio / Partner que refirió o gestiona este negocio (null si es cliente directo)
    /// </summary>
    public Guid? PartnerId { get; private set; }

    private Tenant() { }

    public static Tenant Crear(string nombre, Guid? partnerId = null, Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del tenant es obligatorio.", nameof(nombre));

        return new Tenant
        {
            Id = id ?? Guid.NewGuid(),
            Nombre = nombre.Trim(),
            PartnerId = partnerId,
            Activo = true
        };
    }

    public void ActualizarNombre(string nuevoNombre)
    {
        if (string.IsNullOrWhiteSpace(nuevoNombre))
            throw new ArgumentException("El nombre no puede estar vacío.", nameof(nuevoNombre));

        Nombre = nuevoNombre.Trim();
    }

    public void AsignarPartner(Guid partnerId) => PartnerId = partnerId;
    public void Desactivar() => Activo = false;
    public void Activar() => Activo = true;
}
