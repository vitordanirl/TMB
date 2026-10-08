using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orders.Contracts;
using Orders.Domain.Orders;
using Orders.Infrastructure.Messaging;
using Orders.Infrastructure.Observability;
using Orders.Infrastructure.Persistence;

namespace Orders.Worker.Processing;

/// <summary>
/// Etapa 1 do processamento: Pendente → Processando, e agenda a finalização.
/// </summary>
/// <remarks>
/// Idempotência em camadas:
/// 1. Inbox do MassTransit descarta mensagens com MessageId já consumido;
/// 2. guarda de estado: só age se o pedido ainda estiver Pendente (reentrega/duplicata com outro MessageId);
/// 3. concorrência otimista (xmin): consumidores concorrentes não aplicam a mesma transição duas vezes.
/// Mudança de status, histórico e mensagens de saída são gravados na mesma transação (Outbox).
/// </remarks>
public sealed partial class OrderCreatedConsumer(
    OrdersDbContext db,
    TimeProvider clock,
    IOptions<OrderProcessingOptions> options,
    ILogger<OrderCreatedConsumer> logger) : IConsumer<OrderCreated>
{
    private static readonly Uri CompleteOrderProcessingQueue = new($"queue:{QueueNames.CompleteOrderProcessing}");

    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var orderId = context.Message.OrderId;
        OrderTelemetry.TagOrder(orderId);

        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == orderId, context.CancellationToken);

        if (order is null)
        {
            LogOrderNotFound(orderId);
            return;
        }

        if (order.Status != OrderStatus.Pendente)
        {
            LogAlreadyHandled(orderId, order.Status);
            return;
        }

        var now = clock.GetUtcNow();
        var transition = order.TransitionTo(OrderStatus.Processando, now);
        OrderTelemetry.TagOrder(orderId, transition.ToStatus);

        await context.Publish(transition.ToStatusChangedEvent(), context.CancellationToken);
        var completionEndpoint = await context.GetSendEndpoint(CompleteOrderProcessingQueue);
        await completionEndpoint.Send(
            new CompleteOrderProcessing(orderId, now + options.Value.CompletionDelay),
            context.CancellationToken);

        await db.SaveChangesAsync(context.CancellationToken);

        LogProcessingStarted(orderId);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pedido {OrderId} não encontrado; mensagem descartada.")]
    private partial void LogOrderNotFound(Guid orderId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pedido {OrderId} já está {Status}; OrderCreated ignorado (idempotência).")]
    private partial void LogAlreadyHandled(Guid orderId, OrderStatus status);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pedido {OrderId} em processamento.")]
    private partial void LogProcessingStarted(Guid orderId);
}
