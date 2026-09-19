using BillingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingSaaS.Infrastructure.Data.Configurations;

public class SolicitudRenovacionConfiguration : IEntityTypeConfiguration<SolicitudRenovacion>
{
    public void Configure(EntityTypeBuilder<SolicitudRenovacion> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.TenantId)
            .IsRequired();

        builder.HasIndex(s => s.TenantId);
        builder.HasIndex(s => s.Estado);

        builder.Property(s => s.Frecuencia)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.Monto)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.MetodoPago)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.NumeroComprobante)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.BancoOrigen)
            .HasMaxLength(100);

        builder.Property(s => s.ComprobanteUrl)
            .HasMaxLength(500);

        builder.Property(s => s.Estado)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.MotivoRechazo)
            .HasMaxLength(500);

        builder.Property(s => s.Observaciones)
            .HasMaxLength(500);

        builder.HasOne(s => s.Plan)
            .WithMany()
            .HasForeignKey(s => s.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

