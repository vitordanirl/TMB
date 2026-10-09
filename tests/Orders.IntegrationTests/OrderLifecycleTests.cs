using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Orders.Domain.Orders;
using Orders.IntegrationTests.Infrastructure;

namespace Orders.IntegrationTests;

/// <summary>Fluxo completo com dependências reais: API → Outbox → RabbitMQ → Worker → PostgreSQL.</summary>
[Collection(IntegrationTestEnvironment.Collection)]
public sealed class OrderLifecycleTests(IntegrationTestEnvironment environment)
{
    private readonly HttpClient _api = environment.Api.CreateClient();

    [Fact]
    public async Task PedidoCriado_DeveSerProcessadoAteFinalizado_NaSequenciaObrigatoria()
    {
        var created = await _api.CreateOrderAsync("Maria Silva", "Notebook", 4599.90m);

        created.Status.Should().Be(OrderStatus.Pendente);

        var finished = await _api.WaitForStatusAsync(created.Id, OrderStatus.Finalizado);

        finished.ShouldHaveCompleteHistory();
        finished.Historico.Select(h => h.StatusAnterior).Should().Equal(null, OrderStatus.Pendente, OrderStatus.Processando);
        finished.Historico.Select(h => h.OcorridoEm).Should().BeInAscendingOrder();
        finished.DataAtualizacao.Should().Be(finished.Historico[^1].OcorridoEm);

        var processing = finished.Historico.Single(h => h.StatusNovo == OrderStatus.Processando).OcorridoEm;
        var completion = finished.Historico.Single(h => h.StatusNovo == OrderStatus.Finalizado).OcorridoEm;
        (completion - processing).Should().BeGreaterThanOrEqualTo(IntegrationTestEnvironment.CompletionDelay);
    }

    [Fact]
    public async Task PedidoCriado_DevePassarPorProcessandoAntesDeFinalizar()
    {
        var created = await _api.CreateOrderAsync("João", "Mouse", 89.90m);

        // Com atraso de 1 s entre as etapas, o estado intermediário é observável pela API.
        var processing = await _api.WaitForStatusAsync(created.Id, OrderStatus.Processando);

        processing.Statuses().Should().Equal(OrderStatus.Pendente, OrderStatus.Processando);
    }

    [Fact]
    public async Task CriarPedido_DeveRetornar201ComLocation()
    {
        var response = await _api.PostAsync(
            new Uri("/orders", UriKind.Relative),
            JsonContent("""{ "cliente": "Ana", "produto": "Teclado", "valor": 250 }"""));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.OriginalString.Should().MatchRegex("^/orders/[0-9a-f-]{36}$");
    }

    [Fact]
    public async Task ListarPedidos_DeveRetornarMaisRecentesPrimeiroComPaginacao()
    {
        var first = await _api.CreateOrderAsync("Lista", "Primeiro", 1m);
        var second = await _api.CreateOrderAsync("Lista", "Segundo", 2m);

        var page = await _api.GetFromJsonAsync<Orders.Api.Features.Orders.PagedResponse<Orders.Api.Features.Orders.OrderResponse>>(
            new Uri("/orders?page=1&page_size=2", UriKind.Relative), OrdersApi.Json);

        page!.PageSize.Should().Be(2);
        page.TotalCount.Should().BeGreaterThanOrEqualTo(2);
        page.Items.Select(o => o.Id).Should().Equal(second.Id, first.Id);
    }

    private static StringContent JsonContent(string json) => new(json, System.Text.Encoding.UTF8, "application/json");
}
