using BillingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingSaaS.Infrastructure.Data.Configurations;

public class FacturaConfiguration : IEntityTypeConfiguration<Factura>
{
    public void Configure(EntityTypeBuilder<Factura> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.RazonSocial)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(f => f.Ruc)
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(f => f.ClaveAcceso)
            .HasMaxLength(49)
            .IsRequired();

        builder.Property(f => f.Estado)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(f => f.NumeroAutorizacion)
            .HasMaxLength(49);

        builder.Property(f => f.MensajeErrorSri)
            .HasMaxLength(1000);

        builder.Property(f => f.XmlFirmado);

        builder.Property(f => f.ContribuyenteRimpe)
            .HasMaxLength(60);

        builder.Property(f => f.TotalSinImpuestos)
            .HasPrecision(18, 2);

        builder.Property(f => f.TotalDescuento)
            .HasPrecision(18, 2);

        builder.Property(f => f.ImporteTotal)
            .HasPrecision(18, 2);

        builder.Property(f => f.FormaPago)
            .HasMaxLength(2)
            .IsRequired()
            .HasDefaultValue("01");

        builder.Property(f => f.Plazo)
            .HasPrecision(14, 2);

        builder.Property(f => f.UnidadTiempo)
            .HasMaxLength(20);

        builder.HasOne<Emisor>()
            .WithMany()
            .HasForeignKey(f => f.EmisorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(f => f.Detalles)
            .WithOne()
            .HasForeignKey("FacturaId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Cliente)
            .WithMany()
            .HasForeignKey("ClienteId");

        builder.OwnsMany(f => f.CamposAdicionales, a =>
        {
            a.ToJson();
        });
    }
}

