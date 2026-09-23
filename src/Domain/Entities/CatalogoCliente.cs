namespace BillingSaaS.Domain.Entities;

public class CatalogoCliente : BaseAuditableEntity
{
    public new Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string TipoIdentificacion { get; private set; } = null!;
    public string Identificacion { get; private set; } = null!;
    public string RazonSocial { get; private set; } = null!;
    public string? Direccion { get; private set; }
    public string? CorreoElectronico { get; private set; }
    public bool Activo { get; private set; } = true;

    private CatalogoCliente() { }

    public static CatalogoCliente Crear(
        Guid tenantId,
        string tipoIdentificacion,
        string identificacion,
        string razonSocial,
        string? direccion = null,
        string? correoElectronico = null,
        Guid? id = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El TenantId es obligatorio.", nameof(tenantId));

        if (string.IsNullOrWhiteSpace(tipoIdentificacion) || tipoIdentificacion.Trim().Length > 2)
            throw new ArgumentException("El tipo de identificación es obligatorio y debe tener máximo 2 caracteres.", nameof(tipoIdentificacion));

        if (string.IsNullOrWhiteSpace(identificacion) || identificacion.Trim().Length > 20)
            throw new ArgumentException("La identificación es obligatoria y debe tener máximo 20 caracteres.", nameof(identificacion));

        if (string.IsNullOrWhiteSpace(razonSocial) || razonSocial.Trim().Length > 300)
            throw new ArgumentException("La razón social es obligatoria y debe tener máximo 300 caracteres.", nameof(razonSocial));

        if (direccion is { Length: > 300 })
            throw new ArgumentException("La dirección no puede exceder los 300 caracteres.", nameof(direccion));

        return new CatalogoCliente
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            TipoIdentificacion = tipoIdentificacion.Trim(),
            Identificacion = identificacion.Trim(),
            RazonSocial = razonSocial.Trim(),
            Direccion = string.IsNullOrWhiteSpace(direccion) ? null : direccion.Trim(),
            CorreoElectronico = string.IsNullOrWhiteSpace(correoElectronico) ? null : correoElectronico.Trim(),
            Activo = true
        };
    }

    public void ActualizarContacto(string razonSocial, string? direccion, string? correoElectronico, string? tipoIdentificacion = null)
    {
        if (string.IsNullOrWhiteSpace(razonSocial) || razonSocial.Trim().Length > 300)
            throw new ArgumentException("La razón social es obligatoria y debe tener máximo 300 caracteres.", nameof(razonSocial));

        if (direccion is { Length: > 300 })
            throw new ArgumentException("La dirección no puede exceder los 300 caracteres.", nameof(direccion));

        if (!string.IsNullOrWhiteSpace(tipoIdentificacion))
        {
            if (tipoIdentificacion.Trim().Length > 2)
                throw new ArgumentException("El tipo de identificación debe tener máximo 2 caracteres.", nameof(tipoIdentificacion));

            TipoIdentificacion = tipoIdentificacion.Trim();
        }

        RazonSocial = razonSocial.Trim();
        Direccion = string.IsNullOrWhiteSpace(direccion) ? null : direccion.Trim();
        CorreoElectronico = string.IsNullOrWhiteSpace(correoElectronico) ? null : correoElectronico.Trim();
    }

    public void Desactivar() => Activo = false;

    public void Activar() => Activo = true;
}

