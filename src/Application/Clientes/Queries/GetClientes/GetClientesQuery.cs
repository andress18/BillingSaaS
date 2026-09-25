using BillingSaaS.Application.Clientes.Queries.SearchClientes;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Clientes.Queries.GetClientes;

public record GetClientesQuery(string? Term = null, bool SoloActivos = true, Guid? TenantId = null) : IRequest<List<ClienteLookupDto>>;

public class GetClientesQueryHandler : IRequestHandler<GetClientesQuery, List<ClienteLookupDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public GetClientesQueryHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<List<ClienteLookupDto>> Handle(GetClientesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.CatalogoClientes.AsNoTracking();

        var isAdmin = _user.Roles?.Contains(Roles.Administrator) == true;
        if (!isAdmin)
        {
            if (_user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty)
            {
                query = query.Where(c => c.TenantId == _user.TenantId.Value);
            }
        }
        else if (request.TenantId.HasValue)
        {
            query = query.Where(c => c.TenantId == request.TenantId.Value);
        }

        if (request.SoloActivos)
        {
            query = query.Where(c => c.Activo);
        }

        if (!string.IsNullOrWhiteSpace(request.Term))
        {
            var term = request.Term.Trim();
            query = query.Where(c => c.Identificacion.Contains(term) || c.RazonSocial.Contains(term));
        }

        return await query
            .OrderBy(c => c.RazonSocial)
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
