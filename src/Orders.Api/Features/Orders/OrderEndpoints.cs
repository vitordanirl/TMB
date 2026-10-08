using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orders.Domain.Orders;
using Orders.Infrastructure.Messaging;
using Orders.Infrastructure.Persistence;

namespace Orders.Api.Features.Orders;

public static class OrderEndpoints
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders").WithTags("Pedidos");

        group.MapPost("/", CreateAsync)
            .WithName("CreateOrder")
            .WithSummary("Cria um novo pedido")
            .ProducesValidationProblem();

        group.MapGet("/", ListAsync)
            .WithName("ListOrders")
            .WithSummary("Lista os pedidos (mais recentes primeiro)");

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetOrder")
            .WithSummary("Obtém os detalhes de um pedido, incluindo o histórico de status");

        return app;
    }

    internal static async Task<Created<OrderResponse>> CreateAsync(
        CreateOrderRequest request,
        OrdersDbContext db,
        IPublishEndpoint publishEndpoint,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        // Os campos já foram validados pelo pipeline (AddValidation); o domínio revalida as invariantes.
        var order = Order.Create(request.Cliente!, request.Produto!, request.Valor!.Value, clock.GetUtcNow());

        db.Orders.Add(order);

        // Bus Outbox: a mensagem é gravada na tabela outbox_message e commitada junto com o pedido
        // em SaveChanges; um serviço em background a entrega ao RabbitMQ. Sem pedido órfão nem mensagem perdida.
        await publishEndpoint.Publish(order.ToCreatedEvent(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/orders/{order.Id}", order.ToResponse());
    }

    internal static async Task<Results<Ok<PagedResponse<OrderResponse>>, ValidationProblem>> ListAsync(
        OrdersDbContext db,
        CancellationToken cancellationToken,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery(Name = "page_size")] int pageSize = DefaultPageSize)
    {
        var errors = new Dictionary<string, string[]>();

        if (page < 1)
        {
            errors["page"] = ["Deve ser maior ou igual a 1."];
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            errors["page_size"] = [$"Deve estar entre 1 e {MaxPageSize}."];
        }

        OrderStatus? statusFilter = null;
        if (status is not null)
        {
            if (TryParseStatus(status, out var parsed))
            {
                statusFilter = parsed;
            }
            else
            {
                errors["status"] = [$"Valores aceitos: {string.Join(", ", Enum.GetNames<OrderStatus>())}."];
            }
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var query = db.Orders.AsNoTracking();
        if (statusFilter is { } filter)
        {
            query = query.Where(o => o.Status == filter);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderResponse(o.Id, o.Customer, o.Product, o.Amount, o.Status, o.CreatedAt, o.UpdatedAt))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new PagedResponse<OrderResponse>(items, page, pageSize, totalCount));
    }

    internal static async Task<Results<Ok<OrderDetailsResponse>, NotFound>> GetByIdAsync(
        Guid id,
        OrdersDbContext db,
        CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new OrderDetailsResponse(
                o.Id,
                o.Customer,
                o.Product,
                o.Amount,
                o.Status,
                o.CreatedAt,
                o.UpdatedAt,
                o.StatusHistory
                    .OrderBy(h => h.OccurredAt)
                    .ThenBy(h => h.Id)
                    .Select(h => new OrderStatusHistoryResponse(h.FromStatus, h.ToStatus, h.OccurredAt))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);

        return order is null ? TypedResults.NotFound() : TypedResults.Ok(order);
    }

    // Aceita apenas o nome do status (sem diferenciar maiúsculas); rejeita valores numéricos como "1",
    // que Enum.TryParse aceitaria.
    private static bool TryParseStatus(string value, out OrderStatus status)
    {
        status = default;

        var trimmed = value.Trim();
        if (trimmed.Length == 0 || !char.IsLetter(trimmed[0]))
        {
            return false;
        }

        return Enum.TryParse(trimmed, ignoreCase: true, out status) && Enum.IsDefined(status);
    }
}
