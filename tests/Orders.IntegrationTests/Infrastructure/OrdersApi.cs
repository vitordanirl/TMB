using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using Orders.Api.Features.Orders;
using Orders.Domain.Orders;

namespace Orders.IntegrationTests.Infrastructure;

/// <summary>Chamadas tipadas à API usando o mesmo contrato JSON (snake_case, enums como texto).</summary>
public static class OrdersApi
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<OrderResponse> CreateOrderAsync(this HttpClient client, string cliente, string produto, decimal valor)
    {
        var response = await client.PostAsJsonAsync(new Uri("/orders", UriKind.Relative), new { cliente, produto, valor }, Json);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<OrderResponse>(Json))!;
    }

    public static async Task<OrderDetailsResponse> GetOrderAsync(this HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<OrderDetailsResponse>(new Uri($"/orders/{id}", UriKind.Relative), Json))!;

    public static Task<OrderDetailsResponse> WaitForStatusAsync(this HttpClient client, Guid id, OrderStatus status) =>
        Eventually.Until(() => client.GetOrderAsync(id), order => order.Status == status);

    public static IEnumerable<OrderStatus> Statuses(this OrderDetailsResponse order) =>
        order.Historico.Select(entry => entry.StatusNovo);

    public static void ShouldHaveCompleteHistory(this OrderDetailsResponse order) =>
        order.Statuses().Should().Equal(OrderStatus.Pendente, OrderStatus.Processando, OrderStatus.Finalizado);
}
