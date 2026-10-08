using Orders.Domain.Exceptions;

namespace Orders.Domain.Orders;

public sealed class Order
{
    public const int CustomerMaxLength = 200;
    public const int ProductMaxLength = 200;

    private readonly List<OrderStatusHistory> _statusHistory = [];

    public Guid Id { get; private set; }

    public string Customer { get; private set; } = null!;

    public string Product { get; private set; } = null!;

    public decimal Amount { get; private set; }

    public OrderStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<OrderStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    // EF Core
    private Order()
    {
    }

    public static Order Create(string customer, string product, decimal amount, DateTimeOffset now)
    {
        var order = new Order
        {
            Id = Guid.CreateVersion7(now),
            Customer = Required(customer, nameof(Customer), CustomerMaxLength),
            Product = Required(product, nameof(Product), ProductMaxLength),
            Amount = amount > 0
                ? decimal.Round(amount, 2, MidpointRounding.AwayFromZero)
                : throw new DomainValidationException(nameof(Amount), "O valor do pedido deve ser maior que zero."),
            Status = OrderStatus.Pendente,
            CreatedAt = now,
            UpdatedAt = now,
        };

        order._statusHistory.Add(new OrderStatusHistory(order.Id, fromStatus: null, OrderStatus.Pendente, now));
        return order;
    }

    /// <summary>Indica se <paramref name="next"/> é o próximo passo válido na sequência de status.</summary>
    public bool CanTransitionTo(OrderStatus next) => (Status, next) switch
    {
        (OrderStatus.Pendente, OrderStatus.Processando) => true,
        (OrderStatus.Processando, OrderStatus.Finalizado) => true,
        _ => false,
    };

    /// <summary>
    /// Avança o pedido para o próximo status, registrando o histórico.
    /// Qualquer salto ou retrocesso na sequência Pendente → Processando → Finalizado é rejeitado.
    /// </summary>
    /// <returns>O registro de histórico da transição.</returns>
    /// <exception cref="InvalidStatusTransitionException">Quando a transição não é permitida.</exception>
    public OrderStatusHistory TransitionTo(OrderStatus next, DateTimeOffset now)
    {
        if (!CanTransitionTo(next))
        {
            throw new InvalidStatusTransitionException(Id, Status, next);
        }

        var entry = new OrderStatusHistory(Id, Status, next, now);
        _statusHistory.Add(entry);
        Status = next;
        UpdatedAt = now;

        return entry;
    }

    private static string Required(string value, string field, int maxLength)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainValidationException(field, "Campo obrigatório.");
        }

        return trimmed.Length <= maxLength
            ? trimmed
            : throw new DomainValidationException(field, $"Deve ter no máximo {maxLength} caracteres.");
    }
}
