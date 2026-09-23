using System.Reflection;
using BillingSaaS.Application.Common.Interfaces;
using BillingSaaS.Domain.Entities;
using BillingSaaS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BillingSaaS.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    private readonly IUser? _user;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IUser? user = null) : base(options)
    {
        _user = user;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Emisor> Emisores => Set<Emisor>();
    public DbSet<Factura> Facturas => Set<Factura>();
    public DbSet<NotaDebito> NotasDebito => Set<NotaDebito>();
    public DbSet<NotaCredito> NotasCredito => Set<NotaCredito>();
    public DbSet<Plan> Planes => Set<Plan>();
    public DbSet<TenantSubscription> Suscripciones => Set<TenantSubscription>();
    public DbSet<SolicitudRenovacion> SolicitudesRenovacion => Set<SolicitudRenovacion>();
    public DbSet<CatalogoCliente> CatalogoClientes => Set<CatalogoCliente>();
    public DbSet<CatalogoProducto> CatalogoProductos => Set<CatalogoProducto>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Filtro global multi-tenant por TenantId
        builder.Entity<CatalogoCliente>()
            .HasQueryFilter(c => _user == null || !_user.TenantId.HasValue || _user.TenantId.Value == Guid.Empty || c.TenantId == _user.TenantId.Value);

        builder.Entity<CatalogoProducto>()
            .HasQueryFilter(p => _user == null || !_user.TenantId.HasValue || _user.TenantId.Value == Guid.Empty || p.TenantId == _user.TenantId.Value);
    }
}
