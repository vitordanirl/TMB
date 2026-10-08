using Microsoft.AspNetCore.SignalR;

namespace Orders.Api.Realtime;

/// <summary>
/// Hub de notificações de pedidos (somente servidor → cliente).
/// Os eventos são repassados a todos os clientes conectados por <see cref="OrderNotificationsConsumer"/>.
/// </summary>
public sealed class OrdersHub : Hub<IOrdersClient>
{
    public const string Path = "/hubs/orders";
}

/// <summary>Métodos invocados no cliente (nomes dos eventos recebidos pelo frontend).</summary>
public interface IOrdersClient
{
    Task OrderCreated(OrderCreatedNotification notification);

    Task OrderStatusChanged(OrderStatusChangedNotification notification);
}

// Payloads em snake_case (ver AddJsonProtocol em Program.cs), alinhados ao contrato REST.

public sealed record OrderCreatedNotification(
    Guid OrderId,
    string Cliente,
    string Produto,
    decimal Valor,
    DateTimeOffset DataCriacao);

public sealed record OrderStatusChangedNotification(
    Guid OrderId,
    string? StatusAnterior,
    string StatusNovo,
    DateTimeOffset OcorridoEm);
