using BillingSaaS.Application.Clientes.Queries.SearchClientes;
using BillingSaaS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Clientes.Queries.GetClienteById;

public record GetClienteByIdQuery(Guid Id) : IRequest<ClienteLookupDto?>;

public class GetClienteByIdQueryHandler : IRequestHandler<GetClienteByIdQuery, ClienteLookupDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public GetClienteByIdQueryHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<ClienteLookupDto?> Handle(GetClienteByIdQuery request, CancellationToken cancellationToken)
    {
        var query = _context.CatalogoClientes.AsNoTracking().Where(c => c.Id == request.Id);

        if (_user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty)
        {
            query = query.Where(c => c.TenantId == _user.TenantId.Value);
        }

        return await query
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
            .FirstOrDefaultAsync(cancellationToken);
    }
}

