using BillingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingSaaS.Infrastructure.Data.Configurations;

public class CatalogoProductoConfiguration : IEntityTypeConfiguration<CatalogoProducto>
{
    public void Configure(EntityTypeBuilder<CatalogoProducto> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.TenantId)
            .IsRequired();

        builder.Property(p => p.CodigoPrincipal)
            .HasMaxLength(25)
            .IsRequired();

        builder.Property(p => p.Descripcion)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(p => p.PrecioUnitario)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(p => p.CodigoImpuesto)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(p => p.CodigoPorcentaje)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(p => p.Tarifa)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(p => p.Activo)
            .IsRequired();

        // Índices de rendimiento para búsquedas y autocompletado por tenant
        builder.HasIndex(p => new { p.TenantId, p.CodigoPrincipal });
        builder.HasIndex(p => new { p.TenantId, p.Descripcion });
    }
}

