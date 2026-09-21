using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Productos.Commands.CrearProducto;

public record CrearProductoCommand : IRequest<Guid>
{
    public string CodigoPrincipal { get; init; } = null!;
    public string Descripcion { get; init; } = null!;
    public decimal PrecioUnitario { get; init; }
    public string CodigoImpuesto { get; init; } = "2";
    public string CodigoPorcentaje { get; init; } = "4";
    public decimal Tarifa { get; init; } = 15.00m;
    public Guid? TenantId { get; init; }
}

public class CrearProductoCommandHandler : IRequestHandler<CrearProductoCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public CrearProductoCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<Guid> Handle(CrearProductoCommand request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId.HasValue && request.TenantId.Value != Guid.Empty
            ? request.TenantId.Value
            : (_user.TenantId ?? throw new UnauthorizedAccessException("Usuario no tiene TenantId asignado."));

        var codigoPrincipal = request.CodigoPrincipal.Trim();

        var existe = await _context.CatalogoProductos
            .AnyAsync(p => p.TenantId == tenantId && p.CodigoPrincipal == codigoPrincipal, cancellationToken);

        if (existe)
        {
            throw new InvalidOperationException($"Ya existe un producto con el código '{codigoPrincipal}' en este tenant.");
        }

        var producto = CatalogoProducto.Crear(
            tenantId: tenantId,
            codigoPrincipal: codigoPrincipal,
            descripcion: request.Descripcion,
            precioUnitario: request.PrecioUnitario,
            codigoImpuesto: request.CodigoImpuesto,
            codigoPorcentaje: request.CodigoPorcentaje,
            tarifa: request.Tarifa);

        _context.CatalogoProductos.Add(producto);
        await _context.SaveChangesAsync(cancellationToken);

        return producto.Id;
    }
}

