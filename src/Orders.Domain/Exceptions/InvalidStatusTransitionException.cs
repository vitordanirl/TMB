using Orders.Domain.Orders;

namespace Orders.Domain.Exceptions;

public sealed class InvalidStatusTransitionException(Guid orderId, OrderStatus current, OrderStatus requested)
    : DomainException($"Pedido {orderId}: transição de status inválida de {current} para {requested}.")
{
    public Guid OrderId { get; } = orderId;

    public OrderStatus Current { get; } = current;

    public OrderStatus Requested { get; } = requested;
}
