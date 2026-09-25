using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Application.Productos.Queries.SearchProductos;
using BillingSaaS.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Productos.Queries.GetProductos;

public record GetProductosQuery(string? Term = null, bool SoloActivos = true, Guid? TenantId = null) : IRequest<List<ProductoLookupDto>>;

public class GetProductosQueryHandler : IRequestHandler<GetProductosQuery, List<ProductoLookupDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public GetProductosQueryHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<List<ProductoLookupDto>> Handle(GetProductosQuery request, CancellationToken cancellationToken)
    {
        var query = _context.CatalogoProductos.AsNoTracking();

        var isAdmin = _user.Roles?.Contains(Roles.Administrator) == true;
        if (!isAdmin)
        {
            if (_user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty)
            {
                query = query.Where(p => p.TenantId == _user.TenantId.Value);
            }
        }
        else if (request.TenantId.HasValue)
        {
            query = query.Where(p => p.TenantId == request.TenantId.Value);
        }

        if (request.SoloActivos)
        {
            query = query.Where(p => p.Activo);
        }

        if (!string.IsNullOrWhiteSpace(request.Term))
        {
            var term = request.Term.Trim();
            query = query.Where(p => p.CodigoPrincipal.Contains(term) || p.Descripcion.Contains(term));
        }

        return await query
            .OrderBy(p => p.Descripcion)
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
