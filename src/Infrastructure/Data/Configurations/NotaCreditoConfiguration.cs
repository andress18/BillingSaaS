using BillingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingSaaS.Infrastructure.Data.Configurations;

public class NotaCreditoConfiguration : IEntityTypeConfiguration<NotaCredito>
{
    public void Configure(EntityTypeBuilder<NotaCredito> builder)
    {
        builder.ToTable("NotasCredito");

        builder.HasKey(nc => nc.Id);

        builder.Property(nc => nc.RazonSocial)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(nc => nc.Ruc)
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(nc => nc.ClaveAcceso)
            .HasMaxLength(49)
            .IsRequired();

        builder.HasIndex(nc => nc.ClaveAcceso);
        builder.HasIndex(nc => nc.TenantId);
        builder.HasIndex(nc => nc.EmisorId);

        builder.Property(nc => nc.Estado)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(nc => nc.NumeroAutorizacion)
            .HasMaxLength(49);

        builder.Property(nc => nc.MensajeErrorSri)
            .HasMaxLength(1000);

        builder.Property(nc => nc.XmlFirmado);

        builder.Property(nc => nc.ContribuyenteRimpe)
            .HasMaxLength(60);

        builder.Property(nc => nc.CodDoc)
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(nc => nc.Establecimiento)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(nc => nc.PuntoEmision)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(nc => nc.Secuencial)
            .HasMaxLength(9)
            .IsRequired();

        builder.Property(nc => nc.DireccionMatriz)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(nc => nc.CodDocModificado)
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(nc => nc.NumDocModificado)
            .HasMaxLength(17)
            .IsRequired();

        builder.Property(nc => nc.Motivo)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(nc => nc.TipoIdentificacionComprador)
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(nc => nc.RazonSocialComprador)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(nc => nc.IdentificacionComprador)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(nc => nc.TotalSinImpuestos)
            .HasPrecision(18, 2);

        builder.Property(nc => nc.TotalDescuento)
            .HasPrecision(18, 2);

        builder.Property(nc => nc.ValorModificacion)
            .HasPrecision(18, 2);

        builder.HasOne<Emisor>()
            .WithMany()
            .HasForeignKey(nc => nc.EmisorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(nc => nc.Cliente)
            .WithMany()
            .HasForeignKey(nc => nc.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(nc => nc.Detalles)
            .WithOne()
            .HasForeignKey(d => d.NotaCreditoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

