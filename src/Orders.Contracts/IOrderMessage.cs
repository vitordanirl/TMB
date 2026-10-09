namespace Orders.Contracts;

/// <summary>
/// Mensagem associada a um pedido. Toda mensagem que implementa esta interface é publicada
/// com <c>CorrelationId = OrderId</c> e o header <c>EventType</c> com o nome do tipo.
/// </summary>
public interface IOrderMessage
{
    Guid OrderId { get; }
}
