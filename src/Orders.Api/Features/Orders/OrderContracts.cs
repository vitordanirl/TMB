using System.ComponentModel.DataAnnotations;
using Orders.Domain.Orders;

namespace Orders.Api.Features.Orders;

// Contratos HTTP. Os nomes seguem o desafio (cliente, produto, valor, data_criacao...);
// a serialização usa snake_case (ver Program.cs).

public sealed class CreateOrderRequest
{
    [Required(ErrorMessage = "Campo obrigatório.")]
    [MaxLength(Order.CustomerMaxLength, ErrorMessage = "Deve ter no máximo {1} caracteres.")]
    public string? Cliente { get; init; }

    [Required(ErrorMessage = "Campo obrigatório.")]
    [MaxLength(Order.ProductMaxLength, ErrorMessage = "Deve ter no máximo {1} caracteres.")]
    public string? Produto { get; init; }

    [Required(ErrorMessage = "Campo obrigatório.")]
    [Range(
        typeof(decimal),
        "0.01",
        "9999999999999999.99",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "Deve ser maior que zero e ter no máximo 16 dígitos inteiros.")]
    public decimal? Valor { get; init; }
}

public sealed record OrderResponse(
    Guid Id,
    string Cliente,
    string Produto,
    decimal Valor,
    OrderStatus Status,
    DateTimeOffset DataCriacao,
    DateTimeOffset DataAtualizacao);

public sealed record OrderDetailsResponse(
    Guid Id,
    string Cliente,
    string Produto,
    decimal Valor,
    OrderStatus Status,
    DateTimeOffset DataCriacao,
    DateTimeOffset DataAtualizacao,
    IReadOnlyList<OrderStatusHistoryResponse> Historico);

public sealed record OrderStatusHistoryResponse(
    OrderStatus? StatusAnterior,
    OrderStatus StatusNovo,
    DateTimeOffset OcorridoEm);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

internal static class OrderMappings
{
    public static OrderResponse ToResponse(this Order order) => new(
        order.Id,
        order.Customer,
        order.Product,
        order.Amount,
        order.Status,
        order.CreatedAt,
        order.UpdatedAt);
}
