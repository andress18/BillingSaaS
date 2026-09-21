using BillingSaaS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Clientes.Commands.ActualizarCliente;

public record ActualizarClienteCommand : IRequest
{
    public Guid Id { get; init; }
    public string TipoIdentificacion { get; init; } = null!;
    public string RazonSocial { get; init; } = null!;
    public string? Direccion { get; init; }
    public string? CorreoElectronico { get; init; }
    public bool Activo { get; init; } = true;
}

public class ActualizarClienteCommandHandler : IRequestHandler<ActualizarClienteCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public ActualizarClienteCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(ActualizarClienteCommand request, CancellationToken cancellationToken)
    {
        var query = _context.CatalogoClientes.Where(c => c.Id == request.Id);

        if (_user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty)
        {
            query = query.Where(c => c.TenantId == _user.TenantId.Value);
        }

        var cliente = await query.FirstOrDefaultAsync(cancellationToken);
        Guard.Against.NotFound(request.Id, cliente);

        cliente.ActualizarContacto(
            request.RazonSocial,
            request.Direccion,
            request.CorreoElectronico,
            request.TipoIdentificacion);

        if (request.Activo)
        {
            cliente.Activar();
        }
        else
        {
            cliente.Desactivar();
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}

