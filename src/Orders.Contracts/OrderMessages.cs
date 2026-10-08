namespace Orders.Contracts;

/// <summary>Evento: um pedido foi criado e aguarda processamento.</summary>
public sealed record OrderCreated(
    Guid OrderId,
    string Customer,
    string Product,
    decimal Amount,
    DateTimeOffset CreatedAt) : IOrderMessage;

/// <summary>Evento: o status de um pedido mudou. Os status são enviados pelo nome (ex.: "Processando").</summary>
public sealed record OrderStatusChanged(
    Guid OrderId,
    string? FromStatus,
    string ToStatus,
    DateTimeOffset OccurredAt) : IOrderMessage;

/// <summary>
/// Comando interno do worker: finalizar o processamento de um pedido a partir de <see cref="ProcessAt"/>.
/// </summary>
public sealed record CompleteOrderProcessing(
    Guid OrderId,
    DateTimeOffset ProcessAt) : IOrderMessage;
