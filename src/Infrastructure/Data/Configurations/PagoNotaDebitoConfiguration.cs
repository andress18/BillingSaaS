using BillingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingSaaS.Infrastructure.Data.Configurations;

public class PagoNotaDebitoConfiguration : IEntityTypeConfiguration<PagoNotaDebito>
{
    public void Configure(EntityTypeBuilder<PagoNotaDebito> builder)
    {
        builder.ToTable("PagosNotaDebito");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.FormaPago)
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(p => p.Total)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.Plazo)
            .HasPrecision(14, 2);

        builder.Property(p => p.UnidadTiempo)
            .HasMaxLength(10);
    }
}

