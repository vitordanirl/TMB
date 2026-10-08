using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain.Orders;

namespace Orders.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(o => o.Customer).HasColumnName("cliente").HasMaxLength(Order.CustomerMaxLength).IsRequired();
        builder.Property(o => o.Product).HasColumnName("produto").HasMaxLength(Order.ProductMaxLength).IsRequired();
        builder.Property(o => o.Amount).HasColumnName("valor").HasPrecision(18, 2);

        builder.Property(o => o.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(o => o.CreatedAt).HasColumnName("data_criacao");
        builder.Property(o => o.UpdatedAt).HasColumnName("data_atualizacao");

        // Concorrência otimista via coluna de sistema xmin do PostgreSQL:
        // impede que dois consumidores apliquem a mesma transição em paralelo.
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasMany(o => o.StatusHistory)
            .WithOne()
            .HasForeignKey(h => h.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(o => o.StatusHistory).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.CreatedAt).IsDescending();
    }
}
