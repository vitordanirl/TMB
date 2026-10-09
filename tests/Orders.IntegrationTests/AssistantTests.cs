using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Orders.Api.Features.Ai;
using Orders.Domain.Orders;
using Orders.IntegrationTests.Infrastructure;

namespace Orders.IntegrationTests;

/// <summary>
/// Módulo "Pergunte sobre os pedidos" com o SDK real da Anthropic, um modelo simulado no nível HTTP
/// e as ferramentas consultando o PostgreSQL real.
/// </summary>
[Collection(IntegrationTestEnvironment.Collection)]
public sealed class AssistantTests(IntegrationTestEnvironment environment)
{
    [Fact]
    public async Task Pergunta_DeveConsultarFerramentaComDadosReaisEResponder()
    {
        // Período isolado (data passada) para o resultado não depender dos outros testes.
        await InsertOrdersAsync(new DateOnly(2020, 1, 15), OrderStatus.Pendente, OrderStatus.Pendente, OrderStatus.Finalizado);

        var fake = new FakeAnthropicHandler()
            .RespondWithToolUse(OrderAssistantTools.CountOrders, new { status = "Pendente", data_inicio = "2020-01-15", data_fim = "2020-01-15" })
            .RespondWithText("Em 15/01/2020 havia 2 pedidos pendentes.");
        var api = environment.Api.CreateClientWithFakeAnthropic(fake);

        var answer = await AskAsync(api, "Quantos pedidos pendentes tivemos em 15/01/2020?");

        answer.Resposta.Should().Be("Em 15/01/2020 havia 2 pedidos pendentes.");
        answer.Consultas.Should().ContainSingle().Which.Ferramenta.Should().Be(OrderAssistantTools.CountOrders);

        // A segunda chamada ao modelo leva o resultado real da consulta ao banco.
        var toolResult = fake.Requests[1].LastUserContent!.Single()!;
        toolResult["type"]!.GetValue<string>().Should().Be("tool_result");
        toolResult["content"]!.ToJsonString().Should().Contain("\\u0022total\\u0022:2");

        // Golden: o que a API envia ao modelo (modelo, esforço, fallback, ferramentas e o loop de tool use).
        var requests = new JsonObject
        {
            ["anthropic_beta"] = fake.Requests[0].Betas,
            ["primeira_chamada"] = fake.Requests[0].Body.DeepClone(),
            ["segunda_chamada"] = fake.Requests[1].Body.DeepClone(),
        };
        await VerifyJson(requests.ToJsonString())
            .IgnoreMember("system"); // contém a data/hora atual
    }

    [Fact]
    public async Task TempoMedioDeProcessamento_DeveSerCalculadoPeloHistorico()
    {
        var day = new DateOnly(2020, 2, 10);
        var start = new DateTimeOffset(2020, 2, 10, 12, 0, 0, TimeSpan.FromHours(-3));
        // Pedido 1: espera 2 s + processamento 5 s = 7 s. Pedido 2: espera 4 s + processamento 5 s = 9 s.
        await InsertProcessedOrderAsync(start, waiting: TimeSpan.FromSeconds(2), processing: TimeSpan.FromSeconds(5));
        await InsertProcessedOrderAsync(start.AddMinutes(1), waiting: TimeSpan.FromSeconds(4), processing: TimeSpan.FromSeconds(5));

        var fake = new FakeAnthropicHandler()
            .RespondWithToolUse(OrderAssistantTools.AverageProcessingTime, new { data_inicio = $"{day:yyyy-MM-dd}", data_fim = $"{day:yyyy-MM-dd}" })
            .RespondWithText("O tempo médio foi de 8 segundos.");
        var api = environment.Api.CreateClientWithFakeAnthropic(fake);

        await AskAsync(api, "Qual o tempo médio para aprovar os pedidos em 10/02/2020?");

        var result = JsonNode.Parse(fake.Requests[1].LastUserContent!.Single()!["content"]!.GetValue<string>())!;
        result["pedidos_finalizados"]!.GetValue<int>().Should().Be(2);
        result["tempo_medio_total_segundos"]!.GetValue<double>().Should().Be(8);
        result["tempo_medio_espera_segundos"]!.GetValue<double>().Should().Be(3);
        result["tempo_medio_processamento_segundos"]!.GetValue<double>().Should().Be(5);
    }

    [Fact]
    public async Task ParametroInvalido_DeveVoltarAoModeloComoErroDeFerramenta()
    {
        var fake = new FakeAnthropicHandler()
            .RespondWithToolUse(OrderAssistantTools.CountOrders, new { data_inicio = "ontem" })
            .RespondWithText("Não consegui interpretar a data.");
        var api = environment.Api.CreateClientWithFakeAnthropic(fake);

        var answer = await AskAsync(api, "Quantos pedidos ontem?");

        answer.Consultas.Should().ContainSingle().Which.Erro.Should().BeTrue();
        var toolResult = fake.Requests[1].LastUserContent!.Single()!;
        toolResult["is_error"]!.GetValue<bool>().Should().BeTrue();
        toolResult["content"]!.ToJsonString().Should().Contain("AAAA-MM-DD");
    }

    [Fact]
    public async Task Recusa_DeveRetornarMensagemAmigavel()
    {
        var fake = new FakeAnthropicHandler().RespondWithRefusal();
        var api = environment.Api.CreateClientWithFakeAnthropic(fake);

        var answer = await AskAsync(api, "Pergunta fora do escopo");

        answer.Resposta.Should().StartWith("Não consigo responder");
        answer.Consultas.Should().BeEmpty();
    }

    [Fact]
    public async Task SemChaveDaApi_ModuloFicaDesabilitado()
    {
        var api = environment.Api.CreateClient();

        var status = await api.GetFromJsonAsync<AiStatusResponse>(new Uri("/ai/status", UriKind.Relative), OrdersApi.Json);
        var ask = await api.PostAsJsonAsync(new Uri("/ai/ask", UriKind.Relative), new { pergunta = "Quantos pedidos?" }, OrdersApi.Json);

        status!.Habilitado.Should().BeFalse();
        ask.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PerguntaVazia_DeveRetornar400(string pergunta)
    {
        var api = environment.Api.CreateClientWithFakeAnthropic(new FakeAnthropicHandler());

        var response = await api.PostAsJsonAsync(new Uri("/ai/ask", UriKind.Relative), new { pergunta }, OrdersApi.Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<AssistantAnswer> AskAsync(HttpClient api, string pergunta)
    {
        var response = await api.PostAsJsonAsync(new Uri("/ai/ask", UriKind.Relative), new { pergunta }, OrdersApi.Json);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<AssistantAnswer>(OrdersApi.Json))!;
    }

    private async Task InsertOrdersAsync(DateOnly day, params OrderStatus[] statuses)
    {
        await using var db = environment.CreateDbContext();
        var createdAt = new DateTimeOffset(day.ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(-3)).ToUniversalTime();

        foreach (var status in statuses)
        {
            var order = Order.Create("Analytics", "Teste", 10m, createdAt);
            if (status >= OrderStatus.Processando)
            {
                order.TransitionTo(OrderStatus.Processando, createdAt.AddSeconds(1));
            }

            if (status == OrderStatus.Finalizado)
            {
                order.TransitionTo(OrderStatus.Finalizado, createdAt.AddSeconds(6));
            }

            db.Orders.Add(order);
        }

        await db.SaveChangesAsync();
    }

    private async Task InsertProcessedOrderAsync(DateTimeOffset createdAt, TimeSpan waiting, TimeSpan processing)
    {
        await using var db = environment.CreateDbContext();
        var utc = createdAt.ToUniversalTime();
        var order = Order.Create("Tempo", "Médio", 1m, utc);
        order.TransitionTo(OrderStatus.Processando, utc + waiting);
        order.TransitionTo(OrderStatus.Finalizado, utc + waiting + processing);
        db.Orders.Add(order);
        await db.SaveChangesAsync();
    }
}
