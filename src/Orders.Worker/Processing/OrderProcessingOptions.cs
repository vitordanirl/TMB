using System.ComponentModel.DataAnnotations;

namespace Orders.Worker.Processing;

public sealed class OrderProcessingOptions
{
    public const string SectionName = "OrderProcessing";

    /// <summary>Tempo entre o pedido entrar em "Processando" e ser "Finalizado".</summary>
    [Range(typeof(TimeSpan), "00:00:00", "00:10:00")]
    public TimeSpan CompletionDelay { get; set; } = TimeSpan.FromSeconds(5);
}
