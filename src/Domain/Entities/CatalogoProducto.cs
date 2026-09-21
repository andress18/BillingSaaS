namespace BillingSaaS.Domain.Entities;

public class CatalogoProducto : BaseAuditableEntity
{
    public new Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string CodigoPrincipal { get; private set; } = null!;
    public string Descripcion { get; private set; } = null!;
    public decimal PrecioUnitario { get; private set; }
    public string CodigoImpuesto { get; private set; } = "2"; // 2: IVA
    public string CodigoPorcentaje { get; private set; } = "4"; // 4: 15% SRI vigente
    public decimal Tarifa { get; private set; } = 15.00m;
    public bool Activo { get; private set; } = true;

    private CatalogoProducto() { }

    public static CatalogoProducto Crear(
        Guid tenantId,
        string codigoPrincipal,
        string descripcion,
        decimal precioUnitario,
        string codigoImpuesto = "2",
        string codigoPorcentaje = "4",
        decimal tarifa = 15.00m,
        Guid? id = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El TenantId es obligatorio.", nameof(tenantId));

        if (string.IsNullOrWhiteSpace(codigoPrincipal) || codigoPrincipal.Trim().Length > 25)
            throw new ArgumentException("El código principal es obligatorio y debe tener máximo 25 caracteres.", nameof(codigoPrincipal));

        if (string.IsNullOrWhiteSpace(descripcion) || descripcion.Trim().Length > 300)
            throw new ArgumentException("La descripción es obligatoria y debe tener máximo 300 caracteres.", nameof(descripcion));

        if (precioUnitario < 0)
            throw new ArgumentException("El precio unitario no puede ser negativo.", nameof(precioUnitario));

        if (string.IsNullOrWhiteSpace(codigoImpuesto) || codigoImpuesto.Trim().Length > 10)
            throw new ArgumentException("El código de impuesto es obligatorio y debe tener máximo 10 caracteres.", nameof(codigoImpuesto));

        if (string.IsNullOrWhiteSpace(codigoPorcentaje) || codigoPorcentaje.Trim().Length > 10)
            throw new ArgumentException("El código de porcentaje es obligatorio y debe tener máximo 10 caracteres.", nameof(codigoPorcentaje));

        if (tarifa < 0)
            throw new ArgumentException("La tarifa de impuesto no puede ser negativa.", nameof(tarifa));

        return new CatalogoProducto
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            CodigoPrincipal = codigoPrincipal.Trim(),
            Descripcion = descripcion.Trim(),
            PrecioUnitario = precioUnitario,
            CodigoImpuesto = codigoImpuesto.Trim(),
            CodigoPorcentaje = codigoPorcentaje.Trim(),
            Tarifa = tarifa,
            Activo = true
        };
    }

    public void ActualizarDatos(
        string descripcion,
        decimal precioUnitario,
        string codigoImpuesto,
        string codigoPorcentaje,
        decimal tarifa)
    {
        if (string.IsNullOrWhiteSpace(descripcion) || descripcion.Trim().Length > 300)
            throw new ArgumentException("La descripción es obligatoria y debe tener máximo 300 caracteres.", nameof(descripcion));

        if (precioUnitario < 0)
            throw new ArgumentException("El precio unitario no puede ser negativo.", nameof(precioUnitario));

        if (string.IsNullOrWhiteSpace(codigoImpuesto) || codigoImpuesto.Trim().Length > 10)
            throw new ArgumentException("El código de impuesto es obligatorio y debe tener máximo 10 caracteres.", nameof(codigoImpuesto));

        if (string.IsNullOrWhiteSpace(codigoPorcentaje) || codigoPorcentaje.Trim().Length > 10)
            throw new ArgumentException("El código de porcentaje es obligatorio y debe tener máximo 10 caracteres.", nameof(codigoPorcentaje));

        if (tarifa < 0)
            throw new ArgumentException("La tarifa de impuesto no puede ser negativa.", nameof(tarifa));

        Descripcion = descripcion.Trim();
        PrecioUnitario = precioUnitario;
        CodigoImpuesto = codigoImpuesto.Trim();
        CodigoPorcentaje = codigoPorcentaje.Trim();
        Tarifa = tarifa;
    }

    public void ActualizarPrecio(decimal nuevoPrecio)
    {
        if (nuevoPrecio < 0)
            throw new ArgumentException("El precio unitario no puede ser negativo.", nameof(nuevoPrecio));

        PrecioUnitario = nuevoPrecio;
    }

    public void ActualizarDescripcion(string nuevaDescripcion)
    {
        if (string.IsNullOrWhiteSpace(nuevaDescripcion) || nuevaDescripcion.Trim().Length > 300)
            throw new ArgumentException("La descripción es obligatoria y debe tener máximo 300 caracteres.", nameof(nuevaDescripcion));

        Descripcion = nuevaDescripcion.Trim();
    }

    public void Desactivar() => Activo = false;

    public void Activar() => Activo = true;
}

