namespace Orders.Domain.Orders;

/// <summary>Registro imutável de uma mudança de status do pedido.</summary>
public sealed class OrderStatusHistory
{
    public long Id { get; private set; }

    public Guid OrderId { get; private set; }

    /// <summary>Status anterior; nulo no registro de criação do pedido.</summary>
    public OrderStatus? FromStatus { get; private set; }

    public OrderStatus ToStatus { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    // EF Core
    private OrderStatusHistory()
    {
    }

    internal OrderStatusHistory(Guid orderId, OrderStatus? fromStatus, OrderStatus toStatus, DateTimeOffset occurredAt)
    {
        OrderId = orderId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        OccurredAt = occurredAt;
    }
}
