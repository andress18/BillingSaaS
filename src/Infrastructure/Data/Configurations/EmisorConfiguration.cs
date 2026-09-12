using BillingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingSaaS.Infrastructure.Data.Configurations;

public class EmisorConfiguration : IEntityTypeConfiguration<Emisor>
{
    public void Configure(EntityTypeBuilder<Emisor> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId)
            .IsRequired();

        builder.Property(e => e.Ruc)
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(e => e.RazonSocial)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(e => e.NombreComercial)
            .HasMaxLength(300);

        builder.Property(e => e.DireccionMatriz)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(e => e.DireccionEstablecimiento)
            .HasMaxLength(300);

        builder.Property(e => e.CodigoEstablecimiento)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(e => e.PuntoEmision)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(e => e.RegimenRimpe)
            .HasMaxLength(100);

        builder.Property(e => e.ContribuyenteEspecial)
            .HasMaxLength(50);

        builder.Property(e => e.PasswordCertificado)
            .HasMaxLength(500);

        builder.Property(e => e.SubjectCertificado)
            .HasMaxLength(500);

        // Índice único para evitar duplicidad de punto de emisión por RUC y Tenant
        builder.HasIndex(e => new { e.TenantId, e.Ruc, e.CodigoEstablecimiento, e.PuntoEmision })
            .IsUnique();
    }
}

