using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain.Orders;

namespace Orders.Infrastructure.Persistence.Configurations;

internal sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("order_status_history");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(h => h.OrderId).HasColumnName("order_id");

        builder.Property(h => h.FromStatus)
            .HasColumnName("status_anterior")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(h => h.ToStatus)
            .HasColumnName("status_novo")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(h => h.OccurredAt).HasColumnName("ocorrido_em");

        builder.HasIndex(h => new { h.OrderId, h.OccurredAt });
    }
}
