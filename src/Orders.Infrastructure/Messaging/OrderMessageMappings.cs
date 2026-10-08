using Orders.Contracts;
using Orders.Domain.Orders;

namespace Orders.Infrastructure.Messaging;

public static class OrderMessageMappings
{
    public static OrderCreated ToCreatedEvent(this Order order) => new(
        order.Id,
        order.Customer,
        order.Product,
        order.Amount,
        order.CreatedAt);

    public static OrderStatusChanged ToStatusChangedEvent(this OrderStatusHistory entry) => new(
        entry.OrderId,
        entry.FromStatus?.ToString(),
        entry.ToStatus.ToString(),
        entry.OccurredAt);
}
