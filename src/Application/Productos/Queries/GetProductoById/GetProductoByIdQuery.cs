using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Application.Productos.Queries.SearchProductos;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Productos.Queries.GetProductoById;

public record GetProductoByIdQuery(Guid Id) : IRequest<ProductoLookupDto?>;

public class GetProductoByIdQueryHandler : IRequestHandler<GetProductoByIdQuery, ProductoLookupDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public GetProductoByIdQueryHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<ProductoLookupDto?> Handle(GetProductoByIdQuery request, CancellationToken cancellationToken)
    {
        var query = _context.CatalogoProductos.AsNoTracking().Where(p => p.Id == request.Id);

        if (_user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty)
        {
            query = query.Where(p => p.TenantId == _user.TenantId.Value);
        }

        return await query
            .Select(p => new ProductoLookupDto
            {
                Id = p.Id,
                CodigoPrincipal = p.CodigoPrincipal,
                Descripcion = p.Descripcion,
                PrecioUnitario = p.PrecioUnitario,
                CodigoImpuesto = p.CodigoImpuesto,
                CodigoPorcentaje = p.CodigoPorcentaje,
                Tarifa = p.Tarifa,
                Activo = p.Activo
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}

