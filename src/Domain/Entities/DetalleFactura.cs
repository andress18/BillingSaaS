namespace BillingSaaS.Domain.Entities;

public class DetalleFactura : BaseAuditableEntity
{
    public string CodigoPrincipal { get; private set; } = null!; 
    public string Descripcion { get; private set; } = null!; 
    public decimal Cantidad { get; private set; } 
    public decimal PrecioUnitario { get; private set; } 
    public decimal Descuento { get; private set; } 
    public decimal PrecioTotalSinImpuesto { get; private set; } 

    public IReadOnlyCollection<Impuesto> Impuestos => _impuestos.AsReadOnly(); //[cite: 1]
    private List<Impuesto> _impuestos = new();

    public decimal TotalImpuestos => _impuestos.Sum(i => i.Valor);

    public Guid? CatalogoProductoId { get; private set; }

    private DetalleFactura() { }

    public static DetalleFactura Crear(
        string codigoPrincipal,
        string descripcion,
        decimal cantidad,
        decimal precioUnitario,
        decimal descuento,
        List<Impuesto> impuestos,
        Guid? catalogoProductoId = null)
    {
        if (string.IsNullOrWhiteSpace(codigoPrincipal) || codigoPrincipal.Length > 25)
            throw new ArgumentException("El código principal es obligatorio y debe tener máximo 25 caracteres."); //[cite: 1]

        if (string.IsNullOrWhiteSpace(descripcion) || descripcion.Length > 300)
            throw new ArgumentException("La descripción es obligatoria y debe tener máximo 300 caracteres."); //[cite: 1]

        if (impuestos == null || !impuestos.Any())
            throw new ArgumentException("El detalle debe contener al menos un impuesto asociado."); //[cite: 1]

        var detalle = new DetalleFactura
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
