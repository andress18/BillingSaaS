using System;
using System.Threading;
using System.Threading.Tasks;
using BillingSaaS.Application.Common.Exceptions;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Constants;
using BillingSaaS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Tenants.Commands.ActivarTenant;

public record ActivarTenantCommand : IRequest<ActivarTenantResponseDto>
{
    public Guid? TenantId { get; init; }
    public string? TokenActivacion { get; init; }
    public int DiasVigencia { get; init; } = 365;
}

public record ActivarTenantResponseDto
{
    public Guid TenantId { get; init; }
    public string NombreOrganizacion { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public DateTime FechaActivacion { get; init; }
    public DateTime FechaVencimiento { get; init; }
    public int DiasVigencia { get; init; }
    public string Mensaje { get; init; } = string.Empty;
}

public class ActivarTenantCommandHandler : IRequestHandler<ActivarTenantCommand, ActivarTenantResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly IUser _user;
    private readonly TimeProvider _timeProvider;

    public ActivarTenantCommandHandler(
        IApplicationDbContext context,
        IIdentityService identityService,
        IUser user,
        TimeProvider timeProvider)
    {
        _context = context;
        _identityService = identityService;
        _user = user;
        _timeProvider = timeProvider;
    }

    public async Task<ActivarTenantResponseDto> Handle(ActivarTenantCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _user.Id;
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            throw new UnauthorizedAccessException("Usuario no autenticado.");
        }

        bool isAdmin = await _identityService.IsInRoleAsync(currentUserId, Roles.Administrator);
        if (!isAdmin)
        {
            throw new ForbiddenAccessException();
        }

        Tenant? tenant = null;

        if (request.TenantId.HasValue && request.TenantId.Value != Guid.Empty)
        {
            tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == request.TenantId.Value, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.TokenActivacion))
        {
            var tokenNormalizado = request.TokenActivacion.Trim().ToUpperInvariant();
            tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.TokenActivacion == tokenNormalizado, cancellationToken);
        }
        else
        {
            throw new ArgumentException("Debe proporcionar un TenantId o un Token de Activación válido.");
        }

        if (tenant == null)
        {
            throw new NotFoundException(nameof(Tenant), request.TenantId?.ToString() ?? request.TokenActivacion!);
        }

        var ahoraUtc = _timeProvider.GetUtcNow().UtcDateTime;
        int dias = request.DiasVigencia <= 0 ? 365 : request.DiasVigencia;

        // 1. Activar Tenant
        tenant.Activar(ahoraUtc);

        // 2. Activar Suscripción
        var suscripcion = await _context.Suscripciones
            .FirstOrDefaultAsync(s => s.TenantId == tenant.Id, cancellationToken);

        if (suscripcion != null)
        {
            suscripcion.Activar(ahoraUtc, dias);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var fechaVencimiento = suscripcion?.FechaVencimiento ?? ahoraUtc.AddDays(dias);

        return new ActivarTenantResponseDto
        {
            TenantId = tenant.Id,
            NombreOrganizacion = tenant.Nombre,
            Estado = tenant.Estado,
            FechaActivacion = ahoraUtc,
            FechaVencimiento = fechaVencimiento,
            DiasVigencia = dias,
            Mensaje = $"El cliente '{tenant.Nombre}' ha sido activado exitosamente. Se ha establecido su fecha de vencimiento a {dias} días ({fechaVencimiento:dd/MM/yyyy}) y se ha habilitado la emisión de comprobantes."
        };
    }
}