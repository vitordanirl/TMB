using System.Globalization;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Options;

namespace Orders.Api.Features.Ai;

/// <summary>Uma consulta feita pelo modelo durante a resposta (exibida ao usuário para transparência).</summary>
public sealed record ToolCall(string Ferramenta, IReadOnlyDictionary<string, JsonElement> Argumentos, bool Erro);

public sealed record AssistantAnswer(string Resposta, IReadOnlyList<ToolCall> Consultas);

/// <summary>
/// "Pergunte sobre os pedidos": o modelo interpreta a pergunta, chama ferramentas somente leitura
/// (<see cref="OrderAssistantTools"/>) e responde com base nos dados reais retornados.
/// </summary>
/// <remarks>
/// Loop manual de tool use sobre a Messages API: a cada rodada, os blocos da resposta (inclusive os de
/// raciocínio, que precisam voltar inalterados) são reenviados como turno do assistente, seguidos dos
/// resultados das ferramentas. Usa o fallback do lado do servidor: se um classificador de segurança
/// recusar a requisição, outro modelo a atende na mesma chamada.
/// </remarks>
public sealed partial class OrdersAssistant(
    AnthropicClient client,
    OrderAssistantTools tools,
    BusinessClock clock,
    IOptions<AiOptions> options,
    ILogger<OrdersAssistant> logger)
{
    private const string FallbackBeta = "server-side-fallback-2026-07-01";
    private const int MaxResponseTokens = 16_000;

    private const string RefusalAnswer =
        "Não consigo responder a essa pergunta. Tente perguntar sobre os pedidos (quantidades, valores, status ou tempos de processamento).";

    private const string LimitAnswer =
        "Não consegui concluir a análise dentro do limite de consultas. Tente uma pergunta mais específica.";

    public async Task<AssistantAnswer> AskAsync(string question, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var calls = new List<ToolCall>();
        List<BetaMessageParam> messages = [new() { Role = Role.User, Content = question }];

        for (var round = 0; round < settings.MaxToolRounds; round++)
        {
            var response = await client.Beta.Messages.Create(
                new MessageCreateParams
                {
                    Model = settings.Model,
                    MaxTokens = MaxResponseTokens,
                    Betas = [FallbackBeta],
                    Fallbacks = new Default(), // "default": modelo de fallback recomendado por categoria de recusa
                    OutputConfig = new BetaOutputConfig { Effort = settings.Effort },
                    System = SystemPrompt(),
                    Tools = [.. OrderAssistantTools.Definitions],
                    Messages = messages,
                },
                cancellationToken);

            if (response.StopReason == "refusal")
            {
                LogRefusal(response.StopDetails?.Category?.ToString());
                return new AssistantAnswer(RefusalAnswer, calls);
            }

            List<BetaContentBlockParam> assistantTurn = [];
            List<BetaContentBlockParam> toolResults = [];
            var text = new StringBuilder();

            foreach (var block in response.Content)
            {
                if (block.TryPickText(out var textBlock))
                {
                    text.Append(textBlock.Text);
                    assistantTurn.Add(new BetaTextBlockParam { Text = textBlock.Text });
                }
                else if (block.TryPickThinking(out var thinking))
                {
                    // Assinatura preservada: o modelo valida os blocos de raciocínio reenviados.
                    assistantTurn.Add(new BetaThinkingBlockParam { Thinking = thinking.Thinking, Signature = thinking.Signature });
                }
                else if (block.TryPickRedactedThinking(out var redacted))
                {
                    assistantTurn.Add(new BetaRedactedThinkingBlockParam { Data = redacted.Data });
                }
                else if (block.TryPickFallback(out var fallback))
                {
                    // Marca a troca de modelo após uma recusa; reenviada para manter o turno íntegro.
                    assistantTurn.Add(new BetaFallbackBlockParam
                    {
                        From = new() { Model = fallback.From.Model },
                        To = new() { Model = fallback.To.Model },
                        Trigger = JsonSerializer.SerializeToElement(fallback.Trigger),
                    });
                }
                else if (block.TryPickToolUse(out var toolUse))
                {
                    assistantTurn.Add(new BetaToolUseBlockParam { ID = toolUse.ID, Name = toolUse.Name, Input = toolUse.Input });
                    (string result, bool isError) = await ExecuteToolAsync(toolUse.Name, toolUse.Input, cancellationToken);
                    calls.Add(new ToolCall(toolUse.Name, toolUse.Input, isError));
                    toolResults.Add(new BetaToolResultBlockParam { ToolUseID = toolUse.ID, Content = result, IsError = isError });
                }
            }

            if (response.StopReason != "tool_use" || toolResults.Count == 0)
            {
                var answer = text.ToString().Trim();
                return new AssistantAnswer(answer.Length > 0 ? answer : RefusalAnswer, calls);
            }

            messages.Add(new BetaMessageParam { Role = Role.Assistant, Content = assistantTurn });
            messages.Add(new BetaMessageParam { Role = Role.User, Content = toolResults });
        }

        LogToolRoundLimit(settings.MaxToolRounds);
        return new AssistantAnswer(LimitAnswer, calls);
    }

    private async Task<(string Result, bool IsError)> ExecuteToolAsync(
        string name,
        IReadOnlyDictionary<string, JsonElement> input,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await tools.ExecuteAsync(name, input, cancellationToken), false);
        }
        catch (ToolInputException ex)
        {
            return (ex.Message, true);
        }
    }

    private string SystemPrompt()
    {
        var now = clock.Now;
        var culture = CultureInfo.GetCultureInfo("pt-BR");

        return $"""
            Você é o assistente de análise do sistema de gestão de pedidos. Responda perguntas sobre os pedidos
            usando somente os dados retornados pelas ferramentas disponíveis.

            Contexto:
            - Data e hora atuais: {now.ToString("dddd, dd/MM/yyyy HH:mm", culture)} (horário de Brasília, {BusinessClock.TimeZoneId}). Hoje é {now:yyyy-MM-dd}.
            - Cada pedido tem cliente, produto, valor (R$), status e data de criação.
            - O ciclo de status é Pendente → Processando → Finalizado. "Aprovado", "concluído" ou "processado" equivalem a Finalizado.
            - Períodos como "hoje", "esta semana" e "este mês" referem-se à data de criação, no horário de Brasília.

            Como responder:
            - Consulte as ferramentas antes de afirmar qualquer número; nunca estime nem invente dados.
            - Responda em português do Brasil, de forma direta e amigável, em poucas frases.
            - Formate valores como moeda brasileira (ex.: R$ 1.234,56) e converta durações em segundos para uma forma legível.
            - Se não houver dados para o período, diga isso claramente.
            - Se a pergunta não for sobre os pedidos, explique educadamente que só pode ajudar com informações sobre pedidos.
            """;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pergunta recusada pelo modelo (categoria: {Category}).")]
    private partial void LogRefusal(string? category);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Limite de {Rounds} rodadas de ferramentas atingido sem resposta final.")]
    private partial void LogToolRoundLimit(int rounds);
}
