using BillingSaaS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Productos.Queries.SearchProductos;

public record SearchProductosQuery(string? Term = null, int Limit = 10) : IRequest<List<ProductoLookupDto>>;

public class SearchProductosQueryHandler : IRequestHandler<SearchProductosQuery, List<ProductoLookupDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public SearchProductosQueryHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<List<ProductoLookupDto>> Handle(SearchProductosQuery request, CancellationToken cancellationToken)
    {
        var limit = request.Limit is <= 0 or > 100 ? 10 : request.Limit;

        var query = _context.CatalogoProductos
            .AsNoTracking()
            .Where(p => p.Activo);

        if (_user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty)
        {
            query = query.Where(p => p.TenantId == _user.TenantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Term))
        {
            var term = request.Term.Trim();
            query = query.Where(p => p.CodigoPrincipal.Contains(term) || p.Descripcion.Contains(term));
        }

        return await query
            .OrderBy(p => p.Descripcion)
            .Take(limit)
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
            .ToListAsync(cancellationToken);
    }
}

