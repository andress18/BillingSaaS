using BillingSaaS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Clientes.Queries.SearchClientes;

public record SearchClientesQuery(string? Term = null, int Limit = 10) : IRequest<List<ClienteLookupDto>>;

public class SearchClientesQueryHandler : IRequestHandler<SearchClientesQuery, List<ClienteLookupDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public SearchClientesQueryHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<List<ClienteLookupDto>> Handle(SearchClientesQuery request, CancellationToken cancellationToken)
    {
        var limit = request.Limit is <= 0 or > 100 ? 10 : request.Limit;

        var query = _context.CatalogoClientes
            .AsNoTracking()
            .Where(c => c.Activo);

        if (_user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty)
        {
            query = query.Where(c => c.TenantId == _user.TenantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Term))
        {
            var term = request.Term.Trim();
            query = query.Where(c => c.Identificacion.Contains(term) || c.RazonSocial.Contains(term));
        }

        return await query
            .OrderBy(c => c.RazonSocial)
            .Take(limit)
            .Select(c => new ClienteLookupDto
            {
                Id = c.Id,
                TipoIdentificacion = c.TipoIdentificacion,
                Identificacion = c.Identificacion,
                RazonSocial = c.RazonSocial,
                Direccion = c.Direccion,
                CorreoElectronico = c.CorreoElectronico,
                Activo = c.Activo
            })
            .ToListAsync(cancellationToken);
    }
}

