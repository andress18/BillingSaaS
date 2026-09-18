using System.Reflection;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Emisor> Emisores => Set<Emisor>();
    public DbSet<Factura> Facturas => Set<Factura>();
    public DbSet<Plan> Planes => Set<Plan>();
    public DbSet<TenantSubscription> Suscripciones => Set<TenantSubscription>();


    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
