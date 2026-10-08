using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace Orders.IntegrationTests.Infrastructure;

/// <summary>
/// Fila exclusiva ligada aos exchanges dos eventos, para inspecionar as mensagens exatamente como
/// trafegam no broker (propriedades AMQP, headers e envelope).
/// </summary>
public sealed class RabbitMqSpy : IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _queue;

    private RabbitMqSpy(IConnection connection, IChannel channel, string queue)
    {
        _connection = connection;
        _channel = channel;
        _queue = queue;
    }

    public static async Task<RabbitMqSpy> BindAsync(string connectionString, params string[] exchanges)
    {
        var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
        var connection = await factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();

        var queue = (await channel.QueueDeclareAsync($"spy-{Guid.NewGuid():N}", durable: false, exclusive: true, autoDelete: true)).QueueName;
        foreach (var exchange in exchanges)
        {
            await channel.QueueBindAsync(queue, exchange, routingKey: string.Empty);
        }

        return new RabbitMqSpy(connection, channel, queue);
    }

    /// <summary>Aguarda até haver <paramref name="count"/> mensagens que satisfaçam o filtro.</summary>
    public async Task<IReadOnlyList<CapturedMessage>> WaitForAsync(Func<CapturedMessage, bool> filter, int count = 1)
    {
        var captured = new List<CapturedMessage>();

        await Eventually.Until(
            async () =>
            {
                while (await _channel.BasicGetAsync(_queue, autoAck: true) is { } result)
                {
                    var message = CapturedMessage.From(result);
                    if (filter(message))
                    {
                        captured.Add(message);
                    }
                }

                return captured.Count;
            },
            total => total >= count);

        return captured;
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();
        _channel.Dispose();
        _connection.Dispose();
    }
}

public sealed record CapturedMessage(string Exchange, string? CorrelationId, string? EventType, string Body)
{
    /// <summary>Valor de um campo do conteúdo da mensagem (o objeto "message" do envelope do MassTransit).</summary>
    public string? MessageField(string name)
    {
        using var envelope = JsonDocument.Parse(Body);
        return envelope.RootElement.GetProperty("message").GetProperty(name).GetString();
    }

    public static CapturedMessage From(BasicGetResult result)
    {
        var headers = result.BasicProperties.Headers;
        var eventType = headers is not null && headers.TryGetValue("EventType", out var value) && value is byte[] bytes
            ? Encoding.UTF8.GetString(bytes)
            : null;

        return new CapturedMessage(
            result.Exchange,
            result.BasicProperties.CorrelationId,
            eventType,
            Encoding.UTF8.GetString(result.Body.Span));
    }
}
