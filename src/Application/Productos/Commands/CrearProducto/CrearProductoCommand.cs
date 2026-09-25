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

        var productoExistente = await _context.CatalogoProductos
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.CodigoPrincipal == codigoPrincipal, cancellationToken);

        if (productoExistente != null)
        {
            if (productoExistente.Activo)
            {
                throw new InvalidOperationException($"Ya existe un producto activo con el código '{codigoPrincipal}' en este tenant.");
            }

            var totalActivos = await _context.CatalogoProductos
                .CountAsync(p => p.TenantId == tenantId && p.Activo, cancellationToken);
            if (totalActivos >= 25)
            {
                throw new InvalidOperationException("Ha alcanzado el límite máximo permitido de 25 productos o servicios registrados en su catálogo.");
            }

            // Si fue eliminado previamente (soft-delete), se reactiva con los nuevos datos
            productoExistente.ActualizarDatos(
                request.Descripcion,
                request.PrecioUnitario,
                request.CodigoImpuesto,
                request.CodigoPorcentaje,
                request.Tarifa);

            productoExistente.Activar();
            await _context.SaveChangesAsync(cancellationToken);

            return productoExistente.Id;
        }

        var totalProductos = await _context.CatalogoProductos
            .CountAsync(p => p.TenantId == tenantId && p.Activo, cancellationToken);
        if (totalProductos >= 25)
        {
            throw new InvalidOperationException("Ha alcanzado el límite máximo permitido de 25 productos o servicios registrados en su catálogo.");
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

