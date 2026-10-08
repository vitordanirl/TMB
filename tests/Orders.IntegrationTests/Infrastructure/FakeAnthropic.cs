using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Orders.IntegrationTests.Infrastructure;

/// <summary>
/// Simula a Messages API da Anthropic no nível HTTP: o SDK oficial roda de verdade (serialização,
/// headers, desserialização), mas as respostas são roteirizadas e cada requisição fica registrada.
/// </summary>
public sealed class FakeAnthropicHandler : HttpMessageHandler
{
    private readonly Queue<JsonObject> _responses = new();

    public List<RecordedRequest> Requests { get; } = [];

    public FakeAnthropicHandler RespondWithToolUse(string toolName, object input, string id = "toolu_01") =>
        Enqueue("tool_use", new JsonArray(new JsonObject
        {
            ["type"] = "tool_use",
            ["id"] = id,
            ["name"] = toolName,
            ["input"] = JsonSerializer.SerializeToNode(input),
            ["caller"] = new JsonObject { ["type"] = "direct" },
        }));

    public FakeAnthropicHandler RespondWithText(string text) =>
        Enqueue("end_turn", new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }));

    public FakeAnthropicHandler RespondWithRefusal() =>
        Enqueue("refusal", []);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken);
        var betas = request.Headers.TryGetValues("anthropic-beta", out var values) ? string.Join(",", values) : null;
        Requests.Add(new RecordedRequest(request.RequestUri!.AbsolutePath, betas, JsonNode.Parse(body)!.AsObject()));

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("FakeAnthropicHandler: nenhuma resposta roteirizada restante.");
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responses.Dequeue().ToJsonString(), Encoding.UTF8, "application/json"),
        };
    }

    private FakeAnthropicHandler Enqueue(string stopReason, JsonArray content)
    {
        _responses.Enqueue(new JsonObject
        {
            ["id"] = $"msg_{_responses.Count + Requests.Count + 1:00}",
            ["type"] = "message",
            ["role"] = "assistant",
            ["model"] = "claude-opus-5-5",
            ["content"] = content,
            ["stop_reason"] = stopReason,
            ["stop_sequence"] = null,
            ["stop_details"] = stopReason == "refusal"
                ? new JsonObject { ["type"] = "refusal", ["category"] = "cyber", ["explanation"] = "teste" }
                : null,
            ["usage"] = new JsonObject { ["input_tokens"] = 100, ["output_tokens"] = 20 },
        });

        return this;
    }
}

public sealed record RecordedRequest(string Path, string? Betas, JsonObject Body)
{
    /// <summary>Conteúdo do último turno do usuário (onde vão os tool_results).</summary>
    public JsonArray? LastUserContent => Body["messages"]!.AsArray()[^1]!["content"] as JsonArray;
}

public static class FakeAnthropicExtensions
{
    /// <summary>API com o módulo de IA habilitado e o cliente da Anthropic apontando para o fake.</summary>
    public static HttpClient CreateClientWithFakeAnthropic(this OrdersApiFactory factory, FakeAnthropicHandler handler) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Ai:ApiKey", "test-key");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<AnthropicClient>();
                services.AddSingleton(new AnthropicClient
                {
                    ApiKey = "test-key",
                    HttpClient = new HttpClient(handler),
                    MaxRetries = 0,
                });
            });
        }).CreateClient();
}
