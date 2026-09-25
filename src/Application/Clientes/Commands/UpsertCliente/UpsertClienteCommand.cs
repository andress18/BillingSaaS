using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Clientes.Commands.UpsertCliente;

public record UpsertClienteCommand : IRequest<Guid>
{
    public string TipoIdentificacion { get; init; } = null!;
    public string Identificacion { get; init; } = null!;
    public string RazonSocial { get; init; } = null!;
    public string? Direccion { get; init; }
    public string? CorreoElectronico { get; init; }
    public Guid? TenantId { get; init; }
}

public class UpsertClienteCommandHandler : IRequestHandler<UpsertClienteCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public UpsertClienteCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<Guid> Handle(UpsertClienteCommand request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId.HasValue && request.TenantId.Value != Guid.Empty
            ? request.TenantId.Value
            : (_user.TenantId ?? throw new UnauthorizedAccessException("Usuario no tiene TenantId asignado."));

        var identificacion = request.Identificacion.Trim();

        var existingCliente = await _context.CatalogoClientes
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Identificacion == identificacion, cancellationToken);

        if (existingCliente != null)
        {
            existingCliente.ActualizarContacto(
                request.RazonSocial,
                request.Direccion,
                request.CorreoElectronico,
                request.TipoIdentificacion);

            existingCliente.Activar();
            await _context.SaveChangesAsync(cancellationToken);
            return existingCliente.Id;
        }

        var totalClientes = await _context.CatalogoClientes
            .CountAsync(c => c.TenantId == tenantId && c.Activo, cancellationToken);
        if (totalClientes >= 10)
        {
            throw new InvalidOperationException("Ha alcanzado el límite máximo permitido de 10 compradores registrados en su catálogo.");
        }

        var nuevoCliente = CatalogoCliente.Crear(
            tenantId: tenantId,
            tipoIdentificacion: request.TipoIdentificacion,
            identificacion: identificacion,
            razonSocial: request.RazonSocial,
            direccion: request.Direccion,
            correoElectronico: request.CorreoElectronico);

        _context.CatalogoClientes.Add(nuevoCliente);
        await _context.SaveChangesAsync(cancellationToken);

        return nuevoCliente.Id;
    }
}

