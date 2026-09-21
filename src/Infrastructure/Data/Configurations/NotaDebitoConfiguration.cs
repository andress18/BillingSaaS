using BillingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingSaaS.Infrastructure.Data.Configurations;

public class NotaDebitoConfiguration : IEntityTypeConfiguration<NotaDebito>
{
    public void Configure(EntityTypeBuilder<NotaDebito> builder)
    {
        builder.ToTable("NotasDebito");

        builder.HasKey(nd => nd.Id);

        builder.Property(nd => nd.RazonSocial)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(nd => nd.Ruc)
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(nd => nd.ClaveAcceso)
            .HasMaxLength(49)
            .IsRequired();

        builder.HasIndex(nd => nd.ClaveAcceso);
        builder.HasIndex(nd => nd.TenantId);
        builder.HasIndex(nd => nd.EmisorId);

        builder.Property(nd => nd.Estado)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(nd => nd.NumeroAutorizacion)
            .HasMaxLength(49);

        builder.Property(nd => nd.MensajeErrorSri)
            .HasMaxLength(1000);

        builder.Property(nd => nd.XmlFirmado);

        builder.Property(nd => nd.ContribuyenteRimpe)
            .HasMaxLength(60);

        builder.Property(nd => nd.CodDoc)
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(nd => nd.Establecimiento)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(nd => nd.PuntoEmision)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(nd => nd.Secuencial)
            .HasMaxLength(9)
            .IsRequired();

        builder.Property(nd => nd.DireccionMatriz)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(nd => nd.CodDocModificado)
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(nd => nd.NumDocModificado)
            .HasMaxLength(17)
            .IsRequired();

        builder.Property(nd => nd.TipoIdentificacionComprador)
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(nd => nd.RazonSocialComprador)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(nd => nd.IdentificacionComprador)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(nd => nd.TotalSinImpuestos)
            .HasPrecision(18, 2);

        builder.Property(nd => nd.ValorTotal)
            .HasPrecision(18, 2);

        builder.HasOne<Emisor>()
            .WithMany()
            .HasForeignKey(nd => nd.EmisorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(nd => nd.Cliente)
            .WithMany()
            .HasForeignKey(nd => nd.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(nd => nd.Motivos)
            .WithOne()
            .HasForeignKey(m => m.NotaDebitoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(nd => nd.Impuestos)
            .WithOne()
            .HasForeignKey(i => i.NotaDebitoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(nd => nd.Pagos)
            .WithOne()
            .HasForeignKey(p => p.NotaDebitoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

