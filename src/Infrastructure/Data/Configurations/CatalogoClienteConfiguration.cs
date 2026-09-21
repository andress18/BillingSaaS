using BillingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingSaaS.Infrastructure.Data.Configurations;

public class CatalogoClienteConfiguration : IEntityTypeConfiguration<CatalogoCliente>
{
    public void Configure(EntityTypeBuilder<CatalogoCliente> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId)
            .IsRequired();

        builder.Property(c => c.TipoIdentificacion)
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(c => c.Identificacion)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.RazonSocial)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(c => c.Direccion)
            .HasMaxLength(300);

        builder.Property(c => c.CorreoElectronico)
            .HasMaxLength(256);

        builder.Property(c => c.Activo)
            .IsRequired();

        // Índices de rendimiento para búsquedas y filtros por tenant
        builder.HasIndex(c => new { c.TenantId, c.Identificacion });
        builder.HasIndex(c => new { c.TenantId, c.RazonSocial });
    }
}

