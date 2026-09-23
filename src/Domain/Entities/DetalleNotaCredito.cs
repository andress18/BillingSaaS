namespace BillingSaaS.Domain.Entities;

public class DetalleNotaCredito : BaseAuditableEntity
{
    public int NotaCreditoId { get; private set; }
    public string CodigoPrincipal { get; private set; } = null!;
    public string Descripcion { get; private set; } = null!;
    public decimal Cantidad { get; private set; }
    public decimal PrecioUnitario { get; private set; }
    public decimal Descuento { get; private set; }
    public decimal PrecioTotalSinImpuesto { get; private set; }

    public IReadOnlyCollection<Impuesto> Impuestos => _impuestos.AsReadOnly();
    private List<Impuesto> _impuestos = [];

    public decimal TotalImpuestos => _impuestos.Sum(i => i.Valor);

    public Guid? CatalogoProductoId { get; private set; }

    private DetalleNotaCredito() { }

    public static DetalleNotaCredito Crear(
        string codigoPrincipal,
        string descripcion,
        decimal cantidad,
        decimal precioUnitario,
        decimal descuento,
        List<Impuesto> impuestos,
        Guid? catalogoProductoId = null)
    {
        if (string.IsNullOrWhiteSpace(codigoPrincipal) || codigoPrincipal.Length > 25)
            throw new ArgumentException("El código principal es obligatorio y debe tener máximo 25 caracteres.", nameof(codigoPrincipal));

        if (string.IsNullOrWhiteSpace(descripcion) || descripcion.Length > 300)
            throw new ArgumentException("La descripción es obligatoria y debe tener máximo 300 caracteres.", nameof(descripcion));

        if (cantidad <= 0)
            throw new ArgumentException("La cantidad debe ser mayor a cero.", nameof(cantidad));

        if (precioUnitario < 0)
            throw new ArgumentException("El precio unitario no puede ser negativo.", nameof(precioUnitario));

        if (descuento < 0)
            throw new ArgumentException("El descuento no puede ser negativo.", nameof(descuento));

        if (impuestos == null || !impuestos.Any())
            throw new ArgumentException("El detalle debe contener al menos un impuesto asociado.", nameof(impuestos));

        var detalle = new DetalleNotaCredito
        {
            CodigoPrincipal = codigoPrincipal,
            Descripcion = descripcion,
            Cantidad = cantidad,
            PrecioUnitario = precioUnitario,
            Descuento = descuento,
            PrecioTotalSinImpuesto = (cantidad * precioUnitario) - descuento,
            _impuestos = impuestos,
            CatalogoProductoId = catalogoProductoId
        };

        return detalle;
    }

    public void AsignarCatalogoProducto(Guid catalogoProductoId) => CatalogoProductoId = catalogoProductoId;
}

