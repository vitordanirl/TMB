using MassTransit;
using Microsoft.EntityFrameworkCore;
using Orders.Contracts;
using Orders.Domain.Orders;
using Orders.Infrastructure.Messaging;
using Orders.Infrastructure.Persistence;

namespace Orders.Worker.Processing;

/// <summary>
/// Etapa 2 do processamento: aguarda até <see cref="CompleteOrderProcessing.ProcessAt"/> e muda
/// Processando → Finalizado.
/// </summary>
/// <remarks>
/// A espera é calculada a partir do horário gravado na mensagem, então uma reentrega (ex.: worker
/// reiniciado no meio da espera) aguarda só o tempo restante. Em produção, a espera poderia ser
/// delegada a um agendador (plugin de delayed exchange do RabbitMQ, Quartz) para não ocupar o consumidor.
/// </remarks>
public sealed partial class CompleteOrderProcessingConsumer(
    OrdersDbContext db,
    TimeProvider clock,
    ILogger<CompleteOrderProcessingConsumer> logger) : IConsumer<CompleteOrderProcessing>
{
    public async Task Consume(ConsumeContext<CompleteOrderProcessing> context)
    {
        var orderId = context.Message.OrderId;

        var remaining = context.Message.ProcessAt - clock.GetUtcNow();
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, clock, context.CancellationToken);
        }

        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == orderId, context.CancellationToken);

        if (order is null)
        {
            LogOrderNotFound(orderId);
            return;
        }

        if (order.Status != OrderStatus.Processando)
        {
            LogAlreadyHandled(orderId, order.Status);
            return;
        }

        var transition = order.TransitionTo(OrderStatus.Finalizado, clock.GetUtcNow());

        await context.Publish(transition.ToStatusChangedEvent(), context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);

        LogCompleted(orderId);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pedido {OrderId} não encontrado; mensagem descartada.")]
    private partial void LogOrderNotFound(Guid orderId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pedido {OrderId} está {Status}; CompleteOrderProcessing ignorado (idempotência).")]
    private partial void LogAlreadyHandled(Guid orderId, OrderStatus status);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pedido {OrderId} finalizado.")]
    private partial void LogCompleted(Guid orderId);
}
