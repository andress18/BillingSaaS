using BillingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingSaaS.Infrastructure.Data.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Nombre)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Activo)
            .IsRequired();

        builder.Property(t => t.Estado)
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValue(BillingSaaS.Domain.Constants.TenantEstados.PendientePago);

        builder.Property(t => t.TokenActivacion)
            .HasMaxLength(100);

        builder.Property(t => t.FechaActivacion);

        builder.HasIndex(t => t.TokenActivacion);
    }
}

