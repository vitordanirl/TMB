using Microsoft.EntityFrameworkCore;
using Orders.Domain.Orders;
using Orders.Infrastructure.Persistence;

namespace Orders.Api.Features.Ai;

/// <summary>Período em datas locais (fuso do negócio), com limites inclusivos.</summary>
public sealed record Period(DateOnly? From, DateOnly? To);

/// <summary>
/// Consultas somente leitura usadas pelas ferramentas da IA. A LLM nunca escreve SQL: ela escolhe
/// uma ferramenta e parâmetros validados, e estas consultas (EF Core / SQL parametrizado) fazem o resto.
/// </summary>
public sealed class OrderAnalytics(OrdersDbContext db, BusinessClock clock)
{
    public const int MaxListLimit = 20;

    public async Task<int> CountAsync(OrderStatus? status, Period period, CancellationToken cancellationToken) =>
        await Filter(status, period).CountAsync(cancellationToken);

    public async Task<(int Count, decimal Total)> SumAsync(OrderStatus? status, Period period, CancellationToken cancellationToken)
    {
        var query = Filter(status, period);

        return (await query.CountAsync(cancellationToken), await query.SumAsync(o => o.Amount, cancellationToken));
    }

    public async Task<IReadOnlyList<Order>> ListAsync(OrderStatus? status, Period period, int limit, CancellationToken cancellationToken) =>
        await Filter(status, period)
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Take(Math.Clamp(limit, 1, MaxListLimit))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Tempos médios (em segundos) dos pedidos finalizados criados no período, a partir do histórico:
    /// criação → finalização (total), criação → início do processamento (espera) e processamento → finalização.
    /// </summary>
    public async Task<ProcessingTimes> ProcessingTimesAsync(Period period, CancellationToken cancellationToken)
    {
        var (from, to) = clock.ToUtcRange(period);

        // Agregação no banco; parâmetros interpolados viram parâmetros SQL (sem concatenação).
        var result = await db.Database.SqlQuery<ProcessingTimes>($"""
            SELECT
                COUNT(*)::int AS "FinishedOrders",
                AVG(EXTRACT(EPOCH FROM (f.ocorrido_em - o.data_criacao)))::float8 AS "AverageTotalSeconds",
                AVG(EXTRACT(EPOCH FROM (p.ocorrido_em - o.data_criacao)))::float8 AS "AverageWaitingSeconds",
                AVG(EXTRACT(EPOCH FROM (f.ocorrido_em - p.ocorrido_em)))::float8 AS "AverageProcessingSeconds"
            FROM orders o
            JOIN order_status_history p ON p.order_id = o.id AND p.status_novo = 'Processando'
            JOIN order_status_history f ON f.order_id = o.id AND f.status_novo = 'Finalizado'
            WHERE o.data_criacao >= {from} AND o.data_criacao < {to}
            """).SingleAsync(cancellationToken);

        return result;
    }

    private IQueryable<Order> Filter(OrderStatus? status, Period period)
    {
        var (from, to) = clock.ToUtcRange(period);
        var query = db.Orders.Where(o => o.CreatedAt >= from && o.CreatedAt < to);

        return status is { } value ? query.Where(o => o.Status == value) : query;
    }
}

public sealed record ProcessingTimes(
    int FinishedOrders,
    double? AverageTotalSeconds,
    double? AverageWaitingSeconds,
    double? AverageProcessingSeconds);
