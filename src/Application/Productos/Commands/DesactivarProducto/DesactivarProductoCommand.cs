using BillingSaaS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Productos.Commands.DesactivarProducto;

public record DesactivarProductoCommand(Guid Id) : IRequest;

public class DesactivarProductoCommandHandler : IRequestHandler<DesactivarProductoCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public DesactivarProductoCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(DesactivarProductoCommand request, CancellationToken cancellationToken)
    {
        var query = _context.CatalogoProductos.Where(p => p.Id == request.Id);

        if (_user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty)
        {
            query = query.Where(p => p.TenantId == _user.TenantId.Value);
        }

        var producto = await query.FirstOrDefaultAsync(cancellationToken);
        Guard.Against.NotFound(request.Id, producto);

        producto.Desactivar();
        await _context.SaveChangesAsync(cancellationToken);
    }
}

