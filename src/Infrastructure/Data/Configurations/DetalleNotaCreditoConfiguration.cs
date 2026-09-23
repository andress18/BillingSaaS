using BillingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingSaaS.Infrastructure.Data.Configurations;

public class DetalleNotaCreditoConfiguration : IEntityTypeConfiguration<DetalleNotaCredito>
{
    public void Configure(EntityTypeBuilder<DetalleNotaCredito> builder)
    {
        builder.ToTable("DetallesNotaCredito");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.CodigoPrincipal)
            .HasMaxLength(25)
            .IsRequired();

        builder.Property(d => d.Descripcion)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(d => d.Cantidad)
            .HasPrecision(18, 4);

        builder.Property(d => d.PrecioUnitario)
            .HasPrecision(18, 4);

        builder.Property(d => d.Descuento)
            .HasPrecision(18, 2);

        builder.Property(d => d.PrecioTotalSinImpuesto)
            .HasPrecision(18, 2);

        builder.OwnsMany(d => d.Impuestos, a =>
        {
            a.ToJson();
        });
    }
}

