using BillingSaaS.Domain.Entities;

namespace BillingSaaS.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<Emisor> Emisores { get; }
    DbSet<Factura> Facturas { get; }
    DbSet<NotaDebito> NotasDebito { get; }
    DbSet<Plan> Planes { get; }
    DbSet<TenantSubscription> Suscripciones { get; }
    DbSet<SolicitudRenovacion> SolicitudesRenovacion { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
