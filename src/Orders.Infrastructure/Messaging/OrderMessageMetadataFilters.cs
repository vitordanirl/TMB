using MassTransit;
using Orders.Contracts;

namespace Orders.Infrastructure.Messaging;

/// <summary>
/// Preenche os metadados exigidos para mensagens de pedido:
/// <c>CorrelationId = OrderId</c> e header <c>EventType</c> com o nome do tipo da mensagem.
/// Aplicado tanto a mensagens publicadas quanto enviadas, inclusive as que passam pelo Outbox.
/// </summary>
internal static class OrderMessageMetadata
{
    public static void Apply<T>(SendContext<T> context)
        where T : class
    {
        if (context.Message is not IOrderMessage message)
        {
            return;
        }

        context.CorrelationId = message.OrderId;
        context.Headers.Set(OrderMessageHeaders.EventType, typeof(T).Name);
    }
}

public sealed class OrderMessagePublishFilter<T> : IFilter<PublishContext<T>>
    where T : class
{
    public Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        OrderMessageMetadata.Apply(context);
        return next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("orderMessageMetadata");
}

public sealed class OrderMessageSendFilter<T> : IFilter<SendContext<T>>
    where T : class
{
    public Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
    {
        OrderMessageMetadata.Apply(context);
        return next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("orderMessageMetadata");
}
