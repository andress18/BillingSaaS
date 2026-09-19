using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillingSaaS.Application.Common.Exceptions;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Application.Tenants.Queries.GetClientesCartera;

public record ClienteCarteraDto
{
    public Guid TenantId { get; init; }
    public string NombreOrganizacion { get; init; } = string.Empty;
    public Guid? PartnerId { get; init; }
    public string? PartnerNombre { get; init; }
    public DateTime FechaRegistro { get; init; }

    // Suscripción
    public string PlanCodigo { get; init; } = string.Empty;
    public string PlanNombre { get; init; } = string.Empty;
    public string SuscripcionEstado { get; init; } = string.Empty;
    public DateTime? FechaVencimientoSuscripcion { get; init; }
    public int DiasRestantesSuscripcion { get; init; }

    // Emisor y Firma Electrónica
    public string? Ruc { get; init; }
    public string? RazonSocial { get; init; }
    public bool TieneCertificadoDigital { get; init; }
    public DateTime? FechaCaducidadFirma { get; init; }
    public int? DiasRestantesFirma { get; init; }
    public int FacturasEmitidas { get; init; }
}

public record GetClientesCarteraQuery(Guid? PartnerIdFiltro = null) : IRequest<List<ClienteCarteraDto>>;

public class GetClientesCarteraQueryHandler : IRequestHandler<GetClientesCarteraQuery, List<ClienteCarteraDto>>
{
    private static readonly Guid AdminTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly IUser _user;
    private readonly TimeProvider _timeProvider;

    public GetClientesCarteraQueryHandler(
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

    public async Task<List<ClienteCarteraDto>> Handle(GetClientesCarteraQuery request, CancellationToken cancellationToken)
    {
        var userId = _user.Id ?? throw new UnauthorizedAccessException();
        bool isAdmin = await _identityService.IsInRoleAsync(userId, Roles.Administrator);
        bool isPartner = await _identityService.IsInRoleAsync(userId, Roles.Partner);

        if (!isAdmin && !isPartner)
        {
            throw new ForbiddenAccessException();
        }

        var ahoraUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var tenantsQuery = _context.Tenants.AsNoTracking().AsQueryable();

        // Control de aislamiento de cartera
        if (isPartner && !isAdmin)
        {
            // El partner solo puede ver los clientes que tienen su PartnerId
            var partnerTenantId = _user.TenantId;
            tenantsQuery = tenantsQuery.Where(t => t.PartnerId == partnerTenantId);
        }
        else if (isAdmin && request.PartnerIdFiltro.HasValue)
        {
            tenantsQuery = tenantsQuery.Where(t => t.PartnerId == request.PartnerIdFiltro.Value);
        }
        else if (isAdmin)
        {
            // Excluir el tenant central de la propia administración del SaaS
            tenantsQuery = tenantsQuery.Where(t => t.Id != AdminTenantId);
        }

        var tenantsList = await tenantsQuery.ToListAsync(cancellationToken);
        var tenants = tenantsList.OrderByDescending(t => t.Created).ToList();
        var tenantIds = tenants.Select(t => t.Id).ToList();
        var partnerIds = tenants.Where(t => t.PartnerId.HasValue).Select(t => t.PartnerId!.Value).Distinct().ToList();

        var partnerTenants = await _context.Tenants
            .AsNoTracking()
            .Where(t => partnerIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Nombre, cancellationToken);

        var suscripciones = await _context.Suscripciones
            .AsNoTracking()
            .Include(s => s.Plan)
            .Where(s => tenantIds.Contains(s.TenantId))
            .ToDictionaryAsync(s => s.TenantId, cancellationToken);

        var emisores = await _context.Emisores
            .AsNoTracking()
            .Where(e => tenantIds.Contains(e.TenantId))
            .ToDictionaryAsync(e => e.TenantId, cancellationToken);

        var conteoFacturas = await _context.Facturas
            .AsNoTracking()
            .Where(f => tenantIds.Contains(f.TenantId) && f.Estado != "DEVUELTA")
            .GroupBy(f => f.TenantId)
            .Select(g => new { TenantId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Total, cancellationToken);

        var resultado = new List<ClienteCarteraDto>();

        foreach (var tenant in tenants)
        {
            suscripciones.TryGetValue(tenant.Id, out var sub);
            emisores.TryGetValue(tenant.Id, out var emisor);
            conteoFacturas.TryGetValue(tenant.Id, out int facturasCount);

            string? partnerNombre = null;
            if (tenant.PartnerId.HasValue)
            {
                partnerTenants.TryGetValue(tenant.PartnerId.Value, out partnerNombre);
            }

            int diasRestantesSub = sub?.ObtenerDiasRestantes(ahoraUtc) ?? 0;
            bool tieneFirma = emisor != null && emisor.TieneCertificadoValido();
            DateTime? caducidadFirma = emisor?.FechaCaducidadCertificado;
            int? diasRestantesFirma = caducidadFirma.HasValue
                ? Math.Max(0, (int)(caducidadFirma.Value.Date - ahoraUtc.Date).TotalDays)
                : null;

            resultado.Add(new ClienteCarteraDto
            {
                TenantId = tenant.Id,
                NombreOrganizacion = tenant.Nombre,
                PartnerId = tenant.PartnerId,
                PartnerNombre = partnerNombre,
                FechaRegistro = tenant.Created.UtcDateTime,

                PlanCodigo = sub?.Plan?.Codigo ?? "SIN_PLAN",
                PlanNombre = sub?.Plan?.Nombre ?? "Sin Suscripción",
                SuscripcionEstado = sub?.Estado ?? "INACTIVO",
                FechaVencimientoSuscripcion = sub?.FechaVencimiento,
                DiasRestantesSuscripcion = diasRestantesSub,

                Ruc = emisor?.Ruc,
                RazonSocial = emisor?.RazonSocial,
                TieneCertificadoDigital = tieneFirma,
                FechaCaducidadFirma = caducidadFirma,
                DiasRestantesFirma = diasRestantesFirma,
                FacturasEmitidas = facturasCount
            });
        }

        return resultado;
    }
}
