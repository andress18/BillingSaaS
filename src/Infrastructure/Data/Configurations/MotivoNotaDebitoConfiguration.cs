using BillingSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillingSaaS.Infrastructure.Data.Configurations;

public class MotivoNotaDebitoConfiguration : IEntityTypeConfiguration<MotivoNotaDebito>
{
    public void Configure(EntityTypeBuilder<MotivoNotaDebito> builder)
    {
        builder.ToTable("MotivosNotaDebito");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Razon)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(m => m.Valor)
            .HasPrecision(18, 2)
            .IsRequired();
    }
}

