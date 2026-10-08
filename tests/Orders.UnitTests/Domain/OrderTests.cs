using AwesomeAssertions;
using Orders.Domain.Exceptions;
using Orders.Domain.Orders;

namespace Orders.UnitTests.Domain;

public sealed class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_DeveIniciarPendenteComHistoricoDeCriacao()
    {
        var order = Order.Create("  Maria Silva ", "Notebook", 4599.90m, Now);

        order.Id.Should().NotBeEmpty();
        order.Id.Version.Should().Be(7);
        order.Customer.Should().Be("Maria Silva");
        order.Product.Should().Be("Notebook");
        order.Amount.Should().Be(4599.90m);
        order.Status.Should().Be(OrderStatus.Pendente);
        order.CreatedAt.Should().Be(Now);
        order.UpdatedAt.Should().Be(Now);

        var entry = order.StatusHistory.Should().ContainSingle().Subject;
        entry.OrderId.Should().Be(order.Id);
        entry.FromStatus.Should().BeNull();
        entry.ToStatus.Should().Be(OrderStatus.Pendente);
        entry.OccurredAt.Should().Be(Now);
    }

    [Fact]
    public void Create_DeveArredondarValorParaDuasCasas()
    {
        var order = Order.Create("Cliente", "Produto", 10.005m, Now);

        order.Amount.Should().Be(10.01m);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ComClienteVazio_DeveFalhar(string? customer)
    {
        var act = () => Order.Create(customer!, "Produto", 10m, Now);

        act.Should().Throw<DomainValidationException>().Which.Field.Should().Be(nameof(Order.Customer));
    }

    [Fact]
    public void Create_ComProdutoAcimaDoLimite_DeveFalhar()
    {
        var act = () => Order.Create("Cliente", new string('x', Order.ProductMaxLength + 1), 10m, Now);

        act.Should().Throw<DomainValidationException>().Which.Field.Should().Be(nameof(Order.Product));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_ComValorNaoPositivo_DeveFalhar(decimal amount)
    {
        var act = () => Order.Create("Cliente", "Produto", amount, Now);

        act.Should().Throw<DomainValidationException>().Which.Field.Should().Be(nameof(Order.Amount));
    }

    [Fact]
    public void TransitionTo_DeveSeguirSequenciaCompleta()
    {
        var order = Order.Create("Cliente", "Produto", 10m, Now);

        order.TransitionTo(OrderStatus.Processando, Now.AddSeconds(1));
        order.TransitionTo(OrderStatus.Finalizado, Now.AddSeconds(6));

        order.Status.Should().Be(OrderStatus.Finalizado);
        order.UpdatedAt.Should().Be(Now.AddSeconds(6));
        order.StatusHistory.Select(h => (h.FromStatus, h.ToStatus)).Should().Equal(
            (null, OrderStatus.Pendente),
            (OrderStatus.Pendente, OrderStatus.Processando),
            (OrderStatus.Processando, OrderStatus.Finalizado));
    }

    [Theory]
    [InlineData(OrderStatus.Pendente, OrderStatus.Pendente)]
    [InlineData(OrderStatus.Pendente, OrderStatus.Finalizado)]
    [InlineData(OrderStatus.Processando, OrderStatus.Processando)]
    [InlineData(OrderStatus.Processando, OrderStatus.Pendente)]
    [InlineData(OrderStatus.Finalizado, OrderStatus.Pendente)]
    [InlineData(OrderStatus.Finalizado, OrderStatus.Processando)]
    [InlineData(OrderStatus.Finalizado, OrderStatus.Finalizado)]
    public void TransitionTo_ForaDaSequencia_DeveFalharSemAlterarEstado(OrderStatus current, OrderStatus requested)
    {
        var order = OrderIn(current);
        var historyCount = order.StatusHistory.Count;
        var updatedAt = order.UpdatedAt;

        var act = () => order.TransitionTo(requested, Now.AddMinutes(1));

        act.Should().Throw<InvalidStatusTransitionException>()
            .Which.Should().BeEquivalentTo(new { OrderId = order.Id, Current = current, Requested = requested });
        order.Status.Should().Be(current);
        order.StatusHistory.Should().HaveCount(historyCount);
        order.UpdatedAt.Should().Be(updatedAt);
    }

    [Theory]
    [InlineData(OrderStatus.Pendente, OrderStatus.Processando, true)]
    [InlineData(OrderStatus.Processando, OrderStatus.Finalizado, true)]
    [InlineData(OrderStatus.Pendente, OrderStatus.Finalizado, false)]
    [InlineData(OrderStatus.Finalizado, OrderStatus.Processando, false)]
    public void CanTransitionTo_DeveRefletirSequenciaPermitida(OrderStatus current, OrderStatus next, bool expected)
    {
        OrderIn(current).CanTransitionTo(next).Should().Be(expected);
    }

    private static Order OrderIn(OrderStatus status)
    {
        var order = Order.Create("Cliente", "Produto", 10m, Now);

        if (status >= OrderStatus.Processando)
        {
            order.TransitionTo(OrderStatus.Processando, Now);
        }

        if (status == OrderStatus.Finalizado)
        {
            order.TransitionTo(OrderStatus.Finalizado, Now);
        }

        return order;
    }
}
