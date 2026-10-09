using System.Diagnostics;
using Orders.Domain.Orders;

namespace Orders.Infrastructure.Observability;

/// <summary>Atributos de negócio nos spans, para filtrar traces por pedido no Jaeger (ex.: <c>order.id=...</c>).</summary>
public static class OrderTelemetry
{
    public const string OrderIdTag = "order.id";
    public const string OrderStatusTag = "order.status";

    /// <summary>Marca o span atual (requisição HTTP ou consumo de mensagem) com o pedido e, opcionalmente, o status.</summary>
    public static void TagOrder(Guid orderId, OrderStatus? status = null)
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        activity.SetTag(OrderIdTag, orderId);
        if (status is { } value)
        {
            activity.SetTag(OrderStatusTag, value.ToString());
        }
    }
}
