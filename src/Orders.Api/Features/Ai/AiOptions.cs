using System.ComponentModel.DataAnnotations;

namespace Orders.Api.Features.Ai;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Chave da API da Anthropic (variável de ambiente <c>ANTHROPIC_API_KEY</c>). Sem ela, o módulo fica desativado.</summary>
    public string? ApiKey { get; set; }

    [Required]
    public string Model { get; set; } = "claude-haiku-5-5";

    /// <summary>Esforço de raciocínio (low, medium, high, xhigh, max). Perguntas objetivas sobre dados funcionam bem em "low".</summary>
    [Required]
    public string Effort { get; set; } = "low";

    /// <summary>Limite de rodadas de ferramentas por pergunta (proteção contra loops).</summary>
    [Range(1, 20)]
    public int MaxToolRounds { get; set; } = 6;

    [Range(10, 2000)]
    public int MaxQuestionLength { get; set; } = 500;

    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);
}
