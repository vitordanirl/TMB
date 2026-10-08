using AwesomeAssertions;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orders.Contracts;
using Orders.Domain.Orders;
using Orders.Infrastructure.Messaging;
using Orders.IntegrationTests.Infrastructure;

namespace Orders.IntegrationTests;

/// <summary>Metadados das mensagens, idempotência do consumidor e concorrência, contra o RabbitMQ real.</summary>
[Collection(IntegrationTestEnvironment.Collection)]
public sealed class MessagingTests(IntegrationTestEnvironment environment)
{
    private const string OrderCreatedExchange = "Orders.Contracts:OrderCreated";
    private const string OrderStatusChangedExchange = "Orders.Contracts:OrderStatusChanged";

    private readonly HttpClient _api = environment.Api.CreateClient();

    [Fact]
    public async Task OrderCreated_DeveTerCorrelationIdIgualAoPedidoEEventType()
    {
        await using var spy = await RabbitMqSpy.BindAsync(environment.RabbitMqConnectionString, OrderCreatedExchange);

        var order = await _api.CreateOrderAsync("Carla", "Cadeira", 1899.90m);

        var message = (await spy.WaitForAsync(m => m.CorrelationId == order.Id.ToString())).Single();

        message.EventType.Should().Be("OrderCreated");
        message.Exchange.Should().Be(OrderCreatedExchange);

        // Golden: envelope completo da mensagem como trafega no broker.
        await VerifyJson(message.Body)
            .IgnoreMembers("host", "sourceAddress", "destinationAddress", "sentTime", "conversationId");
    }

    [Fact]
    public async Task OrderStatusChanged_DeveSerPublicadoParaCadaTransicaoComMetadados()
    {
        await using var spy = await RabbitMqSpy.BindAsync(environment.RabbitMqConnectionString, OrderStatusChangedExchange);

        var order = await _api.CreateOrderAsync("Eventos", "Transições", 10m);

        var messages = await spy.WaitForAsync(m => m.CorrelationId == order.Id.ToString(), count: 2);

        messages.Should().AllSatisfy(m => m.EventType.Should().Be("OrderStatusChanged"));
        messages.Select(m => m.MessageField("toStatus")).Should().Equal("Processando", "Finalizado");
    }

    [Fact]
    public async Task MesmaMensagemEntregueDuasVezes_DeveSerProcessadaUmaUnicaVez()
    {
        var order = await InsertPendingOrderAsync();
        var messageId = NewId.NextGuid();

        // Duas entregas com o mesmo MessageId (ex.: reentrega do broker): o Inbox descarta a segunda.
        await PublishOrderCreatedAsync(order, messageId);
        await PublishOrderCreatedAsync(order, messageId);

        var finished = await _api.WaitForStatusAsync(order.Id, OrderStatus.Finalizado);
        await WaitUntilConsumedAsync(messageId);

        finished.ShouldHaveCompleteHistory();
        await using var db = environment.CreateDbContext();
        (await db.Set<InboxState>().CountAsync(i => i.MessageId == messageId)).Should().Be(1);
    }

    [Fact]
    public async Task OrderCreatedRepetidoComOutroMessageId_ParaPedidoFinalizado_DeveSerIgnorado()
    {
        var order = await _api.CreateOrderAsync("Repetido", "Já finalizado", 5m);
        await _api.WaitForStatusAsync(order.Id, OrderStatus.Finalizado);

        // Nova mensagem (outro MessageId): passa pelo Inbox, mas a guarda de status a ignora.
        var messageId = NewId.NextGuid();
        await PublishOrderCreatedAsync(order.Id, messageId);
        await WaitUntilConsumedAsync(messageId);

        var after = await _api.GetOrderAsync(order.Id);
        after.Status.Should().Be(OrderStatus.Finalizado);
        after.ShouldHaveCompleteHistory();
    }

    [Fact]
    public async Task MensagensDuplicadasConcorrentes_DevemGerarUmaUnicaTransicao()
    {
        var order = await InsertPendingOrderAsync();
        var messageIds = Enumerable.Range(0, 5).Select(_ => NewId.NextGuid()).ToArray();

        // 5 cópias com MessageIds diferentes, consumidas em paralelo: a concorrência otimista (xmin)
        // e a guarda de status garantem uma única transição de cada tipo.
        await Task.WhenAll(messageIds.Select(id => PublishOrderCreatedAsync(order, id)));

        var finished = await _api.WaitForStatusAsync(order.Id, OrderStatus.Finalizado);
        foreach (var id in messageIds)
        {
            await WaitUntilConsumedAsync(id);
        }

        finished.ShouldHaveCompleteHistory();
    }

    /// <summary>Pedido gravado direto no banco, sem passar pela API (portanto sem mensagem no Outbox).</summary>
    private async Task<Order> InsertPendingOrderAsync()
    {
        var order = Order.Create("Idempotência", "Teste", 1m, DateTimeOffset.UtcNow);

        await using var db = environment.CreateDbContext();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return order;
    }

    private Task PublishOrderCreatedAsync(Order order, Guid messageId) => PublishOrderCreatedAsync(order.Id, messageId, order);

    private async Task PublishOrderCreatedAsync(Guid orderId, Guid messageId, Order? order = null)
    {
        var bus = environment.Api.Services.GetRequiredService<IBus>();
        var message = order?.ToCreatedEvent() ?? new OrderCreated(orderId, "x", "x", 1m, DateTimeOffset.UtcNow);

        await bus.Publish(message, context => context.MessageId = messageId);
    }

    private Task<bool> WaitUntilConsumedAsync(Guid messageId) =>
        Eventually.Until(
            async () =>
            {
                await using var db = environment.CreateDbContext();
                return await db.Set<InboxState>().AnyAsync(i => i.MessageId == messageId && i.Consumed != null);
            },
            consumed => consumed);
}
