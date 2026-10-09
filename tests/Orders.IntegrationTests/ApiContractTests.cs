using System.Net;
using System.Text;
using AwesomeAssertions;
using Orders.Domain.Orders;
using Orders.IntegrationTests.Infrastructure;

namespace Orders.IntegrationTests;

/// <summary>
/// Golden tests do contrato HTTP: respostas comparadas com snapshots aprovados (Snapshots/*.verified.txt).
/// Qualquer mudança no contrato (campo renomeado, formato de erro, documento OpenAPI) quebra o teste
/// até que o novo snapshot seja revisado e aprovado.
/// </summary>
[Collection(IntegrationTestEnvironment.Collection)]
public sealed class ApiContractTests(IntegrationTestEnvironment environment)
{
    private readonly HttpClient _api = environment.Api.CreateClient();

    [Fact]
    public async Task DocumentoOpenApi()
    {
        var document = await _api.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        await VerifyJson(document);
    }

    [Fact]
    public async Task CriarPedido_Resposta()
    {
        var response = await PostJsonAsync("""{ "cliente": "Golden", "produto": "Snapshot", "valor": 123.45 }""");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        await VerifyJson(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DetalhesDoPedidoFinalizado_Resposta()
    {
        var order = await _api.CreateOrderAsync("Golden", "Detalhes", 99.90m);
        await _api.WaitForStatusAsync(order.Id, OrderStatus.Finalizado);

        var json = await _api.GetStringAsync(new Uri($"/orders/{order.Id}", UriKind.Relative));

        await VerifyJson(json);
    }

    [Fact]
    public async Task CriarPedido_Invalido_RetornaProblemDetails()
    {
        var response = await PostJsonAsync("""{ "cliente": "   ", "valor": 0 }""");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await VerifyJson(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CriarPedido_JsonMalformado_RetornaProblemDetails()
    {
        var response = await PostJsonAsync("""{ "cliente": """);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await VerifyJson(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ListarPedidos_ParametrosInvalidos_RetornaProblemDetails()
    {
        var response = await _api.GetAsync(new Uri("/orders?status=1&page=0&page_size=500", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await VerifyJson(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ObterPedido_Inexistente_Retorna404()
    {
        var response = await _api.GetAsync(new Uri($"/orders/{Guid.Empty}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await VerifyJson(await response.Content.ReadAsStringAsync());
    }

    private Task<HttpResponseMessage> PostJsonAsync(string json) =>
        _api.PostAsync(new Uri("/orders", UriKind.Relative), new StringContent(json, Encoding.UTF8, "application/json"));
}
