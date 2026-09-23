using BillingSaaS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Productos.Commands.ActualizarProducto;

public record ActualizarProductoCommand : IRequest
{
    public Guid Id { get; init; }
    public string Descripcion { get; init; } = null!;
    public decimal PrecioUnitario { get; init; }
    public string CodigoImpuesto { get; init; } = "2";
    public string CodigoPorcentaje { get; init; } = "4";
    public decimal Tarifa { get; init; } = 15.00m;
    public bool Activo { get; init; } = true;
}

public class ActualizarProductoCommandHandler : IRequestHandler<ActualizarProductoCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public ActualizarProductoCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(ActualizarProductoCommand request, CancellationToken cancellationToken)
    {
        var query = _context.CatalogoProductos.Where(p => p.Id == request.Id);

        if (_user.TenantId.HasValue && _user.TenantId.Value != Guid.Empty)
        {
            query = query.Where(p => p.TenantId == _user.TenantId.Value);
        }

        var producto = await query.FirstOrDefaultAsync(cancellationToken);
        Guard.Against.NotFound(request.Id, producto);

        producto.ActualizarDatos(
            request.Descripcion,
            request.PrecioUnitario,
            request.CodigoImpuesto,
            request.CodigoPorcentaje,
            request.Tarifa);

        if (request.Activo)
        {
            producto.Activar();
        }
        else
        {
            producto.Desactivar();
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}

