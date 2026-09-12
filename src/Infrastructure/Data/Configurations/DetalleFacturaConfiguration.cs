using BillingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingSaaS.Infrastructure.Data.Configurations;

public class DetalleFacturaConfiguration : IEntityTypeConfiguration<DetalleFactura>
{
    public void Configure(EntityTypeBuilder<DetalleFactura> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.CodigoPrincipal)
            .HasMaxLength(25)
            .IsRequired();

        builder.Property(d => d.Descripcion)
            .HasMaxLength(300)
            .IsRequired();

        builder.OwnsMany(d => d.Impuestos, a =>
        {
            a.ToJson();
        });
    }
}

