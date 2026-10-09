using System.Threading.RateLimiting;
using Anthropic;
using Anthropic.Exceptions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Orders.Api.Features.Ai;

public sealed record AskRequest(string? Pergunta);

public sealed record AiStatusResponse(bool Habilitado, string? Modelo);

public static partial class AiEndpoints
{
    public const string RateLimitPolicy = "ai";

    public static IServiceCollection AddOrdersAssistant(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.SectionName))
            // Convenção da Anthropic: a chave vem de ANTHROPIC_API_KEY.
            .Configure(options => options.ApiKey ??= configuration["ANTHROPIC_API_KEY"])
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<BusinessClock>();
        services.AddScoped<OrderAnalytics>();
        services.AddScoped<OrderAssistantTools>();
        services.AddScoped<OrdersAssistant>();
        services.AddSingleton(provider => new AnthropicClient
        {
            ApiKey = provider.GetRequiredService<IOptions<AiOptions>>().Value.ApiKey,
        });

        // Cada pergunta custa tokens: limita a taxa por cliente (IP).
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
        });

        return services;
    }

    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/ai").WithTags("IA");

        group.MapGet("/status", (IOptions<AiOptions> options) =>
                TypedResults.Ok(new AiStatusResponse(options.Value.Enabled, options.Value.Enabled ? options.Value.Model : null)))
            .WithName("GetAiStatus")
            .WithSummary("Indica se o módulo de IA está configurado");

        group.MapPost("/ask", AskAsync)
            .WithName("AskAboutOrders")
            .WithSummary("Responde perguntas em linguagem natural sobre os pedidos, consultando os dados reais")
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireRateLimiting(RateLimitPolicy);

        return app;
    }

    internal static async Task<Results<Ok<AssistantAnswer>, ValidationProblem, ProblemHttpResult>> AskAsync(
        AskRequest request,
        IOptions<AiOptions> options,
        OrdersAssistant assistant,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Módulo de IA não configurado.",
                detail: "Defina a variável de ambiente ANTHROPIC_API_KEY para habilitar as perguntas sobre os pedidos.");
        }

        var question = request.Pergunta?.Trim();
        if (string.IsNullOrEmpty(question) || question.Length > settings.MaxQuestionLength)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pergunta"] = [$"Informe uma pergunta com até {settings.MaxQuestionLength} caracteres."],
            });
        }

        try
        {
            return TypedResults.Ok(await assistant.AskAsync(question, cancellationToken));
        }
        catch (AnthropicApiException ex)
        {
            LogProviderError(loggerFactory.CreateLogger(typeof(AiEndpoints)), ex);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "O serviço de IA não respondeu como esperado.",
                detail: "Tente novamente em instantes.");
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao consultar o provedor de IA.")]
    private static partial void LogProviderError(ILogger logger, Exception exception);
}
