namespace BillingSaaS.Application.Productos.Queries.SearchProductos;

public class ProductoLookupDto
{
    public Guid Id { get; init; }
    public string CodigoPrincipal { get; init; } = null!;
    public string Descripcion { get; init; } = null!;
    public decimal PrecioUnitario { get; init; }
    public string CodigoImpuesto { get; init; } = null!;
    public string CodigoPorcentaje { get; init; } = null!;
    public decimal Tarifa { get; init; }
    public bool Activo { get; init; }
}

