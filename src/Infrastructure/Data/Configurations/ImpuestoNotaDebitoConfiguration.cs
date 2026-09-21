using BillingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingSaaS.Infrastructure.Data.Configurations;

public class ImpuestoNotaDebitoConfiguration : IEntityTypeConfiguration<ImpuestoNotaDebito>
{
    public void Configure(EntityTypeBuilder<ImpuestoNotaDebito> builder)
    {
        builder.ToTable("ImpuestosNotaDebito");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Codigo)
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(i => i.CodigoPorcentaje)
            .HasMaxLength(4)
            .IsRequired();

        builder.Property(i => i.Tarifa)
            .HasPrecision(5, 2);

        builder.Property(i => i.BaseImponible)
            .HasPrecision(18, 2);

        builder.Property(i => i.Valor)
            .HasPrecision(18, 2);
    }
}

