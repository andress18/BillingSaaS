using BillingSaaS.Domain.Entities;

namespace BillingSaaS.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<Emisor> Emisores { get; }
    DbSet<Factura> Facturas { get; }
    DbSet<Plan> Planes { get; }
    DbSet<TenantSubscription> Suscripciones { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
