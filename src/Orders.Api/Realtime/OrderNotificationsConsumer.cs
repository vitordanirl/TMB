using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Orders.Contracts;
using Orders.Infrastructure.Messaging;

namespace Orders.Api.Realtime;

/// <summary>Repassa os eventos de pedidos do RabbitMQ para os clientes conectados ao <see cref="OrdersHub"/>.</summary>
public sealed class OrderNotificationsConsumer(IHubContext<OrdersHub, IOrdersClient> hub)
    : IConsumer<OrderCreated>, IConsumer<OrderStatusChanged>
{
    public Task Consume(ConsumeContext<OrderCreated> context)
    {
        var message = context.Message;

        return hub.Clients.All.OrderCreated(new OrderCreatedNotification(
            message.OrderId,
            message.Customer,
            message.Product,
            message.Amount,
            message.CreatedAt));
    }

    public Task Consume(ConsumeContext<OrderStatusChanged> context)
    {
        var message = context.Message;

        return hub.Clients.All.OrderStatusChanged(new OrderStatusChangedNotification(
            message.OrderId,
            message.FromStatus,
            message.ToStatus,
            message.OccurredAt));
    }
}

/// <summary>
/// Cada instância da API precisa receber <b>todos</b> os eventos (fan-out) para notificar os seus
/// próprios clientes SignalR. Por isso a fila é exclusiva da instância (nome com o hostname) e é
/// removida automaticamente quando a instância desconecta. Sem Outbox/Inbox: ver
/// <see cref="MessagingExtensions.NotificationEndpointPrefix"/>.
/// </summary>
public sealed class OrderNotificationsConsumerDefinition : ConsumerDefinition<OrderNotificationsConsumer>
{
    public OrderNotificationsConsumerDefinition()
    {
        EndpointName = $"{MessagingExtensions.NotificationEndpointPrefix}orders-{InstanceName()}";
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<OrderNotificationsConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        if (endpointConfigurator is IRabbitMqReceiveEndpointConfigurator rabbitMq)
        {
            rabbitMq.AutoDelete = true;
        }
    }

    private static string InstanceName() =>
        new string([.. Environment.MachineName.ToLowerInvariant().Where(char.IsAsciiLetterOrDigit)]) is { Length: > 0 } name
            ? name
            : Guid.NewGuid().ToString("N")[..12];
}
