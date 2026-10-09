namespace Orders.Domain.Orders;

/// <summary>
/// Ciclo de vida do pedido. A ordem dos valores reflete a sequência obrigatória:
/// Pendente → Processando → Finalizado.
/// </summary>
public enum OrderStatus
{
    Pendente = 0,
    Processando = 1,
    Finalizado = 2,
}
