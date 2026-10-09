using System.Globalization;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Orders.Domain.Orders;

namespace Orders.Api.Features.Ai;

/// <summary>
/// Ferramentas expostas ao modelo. Todas são somente leitura e recebem parâmetros validados aqui;
/// entradas inválidas viram um <c>tool_result</c> de erro que o modelo pode corrigir.
/// </summary>
public sealed class OrderAssistantTools(OrderAnalytics analytics)
{
    public const string CountOrders = "contar_pedidos";
    public const string SumOrderValue = "somar_valor_pedidos";
    public const string AverageProcessingTime = "tempo_medio_processamento";
    public const string ListOrders = "listar_pedidos";

    private static readonly JsonSerializerOptions ResultJson = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private static readonly JsonElement StatusProperty = Schema(new
    {
        type = "string",
        @enum = Enum.GetNames<OrderStatus>(),
        description = "Filtra pelo status atual do pedido. Omita para considerar todos.",
    });

    private static readonly JsonElement StartDateProperty = Schema(new
    {
        type = "string",
        format = "date",
        description = "Primeiro dia do período (AAAA-MM-DD, inclusivo, horário de Brasília), pela data de criação do pedido. Omita para não limitar o início.",
    });

    private static readonly JsonElement EndDateProperty = Schema(new
    {
        type = "string",
        format = "date",
        description = "Último dia do período (AAAA-MM-DD, inclusivo, horário de Brasília). Omita para não limitar o fim.",
    });

    public static IReadOnlyList<BetaToolUnion> Definitions { get; } =
    [
        new BetaTool
        {
            Name = CountOrders,
            Description = "Conta pedidos, opcionalmente filtrando por status e por período de criação.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["status"] = StatusProperty,
                    ["data_inicio"] = StartDateProperty,
                    ["data_fim"] = EndDateProperty,
                },
            },
        },
        new BetaTool
        {
            Name = SumOrderValue,
            Description = "Soma o valor (R$) dos pedidos e informa quantos foram somados, opcionalmente filtrando por status e período de criação.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["status"] = StatusProperty,
                    ["data_inicio"] = StartDateProperty,
                    ["data_fim"] = EndDateProperty,
                },
            },
        },
        new BetaTool
        {
            Name = AverageProcessingTime,
            Description =
                "Tempos médios dos pedidos já finalizados criados no período, calculados pelo histórico de status: " +
                "total (criação até finalização, isto é, o tempo para aprovar/concluir um pedido), " +
                "espera (criação até início do processamento) e processamento (início do processamento até finalização). " +
                "Valores em segundos.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["data_inicio"] = StartDateProperty,
                    ["data_fim"] = EndDateProperty,
                },
            },
        },
        new BetaTool
        {
            Name = ListOrders,
            Description = "Lista os pedidos mais recentes (cliente, produto, valor, status e data de criação), opcionalmente filtrando por status e período.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["status"] = StatusProperty,
                    ["data_inicio"] = StartDateProperty,
                    ["data_fim"] = EndDateProperty,
                    ["limite"] = Schema(new
                    {
                        type = "integer",
                        minimum = 1,
                        maximum = OrderAnalytics.MaxListLimit,
                        description = $"Quantidade máxima de pedidos (1 a {OrderAnalytics.MaxListLimit}, padrão 10).",
                    }),
                },
            },
        },
    ];

    /// <summary>Executa a ferramenta e devolve o resultado em JSON (ou lança <see cref="ToolInputException"/>).</summary>
    public async Task<string> ExecuteAsync(string name, IReadOnlyDictionary<string, JsonElement> input, CancellationToken cancellationToken)
    {
        object result = name switch
        {
            CountOrders => await CountAsync(input, cancellationToken),
            SumOrderValue => await SumAsync(input, cancellationToken),
            AverageProcessingTime => await ProcessingTimesAsync(input, cancellationToken),
            ListOrders => await ListAsync(input, cancellationToken),
            _ => throw new ToolInputException($"Ferramenta desconhecida: {name}."),
        };

        return JsonSerializer.Serialize(result, ResultJson);
    }

    private async Task<object> CountAsync(IReadOnlyDictionary<string, JsonElement> input, CancellationToken cancellationToken)
    {
        var (status, period) = (ReadStatus(input), ReadPeriod(input));

        return new { Total = await analytics.CountAsync(status, period, cancellationToken), Filtros = Filters(status, period) };
    }

    private async Task<object> SumAsync(IReadOnlyDictionary<string, JsonElement> input, CancellationToken cancellationToken)
    {
        var (status, period) = (ReadStatus(input), ReadPeriod(input));
        var (count, total) = await analytics.SumAsync(status, period, cancellationToken);

        return new { ValorTotal = total, QuantidadePedidos = count, Filtros = Filters(status, period) };
    }

    private async Task<object> ProcessingTimesAsync(IReadOnlyDictionary<string, JsonElement> input, CancellationToken cancellationToken)
    {
        var period = ReadPeriod(input);
        var times = await analytics.ProcessingTimesAsync(period, cancellationToken);

        return new
        {
            PedidosFinalizados = times.FinishedOrders,
            TempoMedioTotalSegundos = Round(times.AverageTotalSeconds),
            TempoMedioEsperaSegundos = Round(times.AverageWaitingSeconds),
            TempoMedioProcessamentoSegundos = Round(times.AverageProcessingSeconds),
            Filtros = Filters(null, period),
        };
    }

    private async Task<object> ListAsync(IReadOnlyDictionary<string, JsonElement> input, CancellationToken cancellationToken)
    {
        var (status, period) = (ReadStatus(input), ReadPeriod(input));
        var limit = input.TryGetValue("limite", out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
            ? parsed
            : 10;

        if (limit is < 1 or > OrderAnalytics.MaxListLimit)
        {
            throw new ToolInputException($"\"limite\" deve estar entre 1 e {OrderAnalytics.MaxListLimit}.");
        }

        var orders = await analytics.ListAsync(status, period, limit, cancellationToken);

        return new
        {
            Pedidos = orders.Select(o => new
            {
                Id = o.Id,
                Cliente = o.Customer,
                Produto = o.Product,
                Valor = o.Amount,
                Status = o.Status.ToString(),
                DataCriacao = o.CreatedAt,
            }),
            Filtros = Filters(status, period),
        };
    }

    private static OrderStatus? ReadStatus(IReadOnlyDictionary<string, JsonElement> input)
    {
        if (!input.TryGetValue("status", out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String && Enum.TryParse<OrderStatus>(value.GetString(), ignoreCase: true, out var status)
            && Enum.IsDefined(status)
            ? status
            : throw new ToolInputException($"\"status\" inválido. Use: {string.Join(", ", Enum.GetNames<OrderStatus>())}.");
    }

    private static Period ReadPeriod(IReadOnlyDictionary<string, JsonElement> input)
    {
        var period = new Period(ReadDate(input, "data_inicio"), ReadDate(input, "data_fim"));

        return period is { From: { } from, To: { } to } && from > to
            ? throw new ToolInputException("\"data_inicio\" deve ser anterior ou igual a \"data_fim\".")
            : period;
    }

    private static DateOnly? ReadDate(IReadOnlyDictionary<string, JsonElement> input, string field)
    {
        if (!input.TryGetValue(field, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new ToolInputException($"\"{field}\" deve estar no formato AAAA-MM-DD.");
    }

    private static object Filters(OrderStatus? status, Period period) => new
    {
        Status = status?.ToString(),
        DataInicio = period.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DataFim = period.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };

    private static double? Round(double? seconds) => seconds is { } value ? Math.Round(value, 1) : null;

    private static JsonElement Schema(object schema) => JsonSerializer.SerializeToElement(schema);
}

/// <summary>Entrada de ferramenta inválida: devolvida ao modelo como <c>tool_result</c> com <c>is_error</c>.</summary>
public sealed class ToolInputException(string message) : Exception(message);
