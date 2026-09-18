using BillingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingSaaS.Infrastructure.Data.Configurations;

public class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Codigo)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(p => p.Codigo)
            .IsUnique();

        builder.Property(p => p.Nombre)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(p => p.Descripcion)
            .HasMaxLength(500);

        builder.Property(p => p.PrecioMensual)
            .HasPrecision(18, 2);

        builder.Property(p => p.PrecioAnual)
            .HasPrecision(18, 2);

        builder.Property(p => p.TiposDocumentosPermitidos)
            .HasMaxLength(100)
            .IsRequired();
    }
}

