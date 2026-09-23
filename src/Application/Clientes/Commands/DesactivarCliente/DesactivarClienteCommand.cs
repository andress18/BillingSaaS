using BillingSaaS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Clientes.Commands.DesactivarCliente;

public record DesactivarClienteCommand(Guid Id) : IRequest;

public class DesactivarClienteCommandHandler : IRequestHandler<DesactivarClienteCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public DesactivarClienteCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(DesactivarClienteCommand request, CancellationToken cancellationToken)
    {
        var query = _context.CatalogoClientes.Where(c => c.Id == request.Id);

        if (_user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty)
        {
            query = query.Where(c => c.TenantId == _user.TenantId.Value);
        }

        var cliente = await query.FirstOrDefaultAsync(cancellationToken);
        Guard.Against.NotFound(request.Id, cliente);

        cliente.Desactivar();
        await _context.SaveChangesAsync(cancellationToken);
    }
}

