using System.Net.Mime;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Orders.Api.Health;

public static class HealthCheckExtensions
{
    /// <summary>Checks executados em /health/ready (dependências externas).</summary>
    public const string ReadyTag = "ready";

    /// <summary>Checks executados em /health/live (o próprio processo).</summary>
    public const string LiveTag = "live";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static IHealthChecksBuilder AddOrdersApiHealthChecks(this IServiceCollection services, string connectionString) =>
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: [LiveTag])
            .AddNpgSql(connectionString, name: "postgres", tags: [ReadyTag], timeout: TimeSpan.FromSeconds(3));

    public static IEndpointRouteBuilder MapOrdersHealthChecks(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(LiveTag),
            ResponseWriter = WriteResponseAsync,
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = WriteResponseAsync,
        });

        return app;
    }

    private static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = MediaTypeNames.Application.Json;

        var payload = new
        {
            Status = report.Status.ToString(),
            TotalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            Checks = report.Entries.Select(entry => new
            {
                Name = entry.Key,
                Status = entry.Value.Status.ToString(),
                DurationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1),
                entry.Value.Description,
                Error = entry.Value.Exception?.Message,
            }),
        };

        return context.Response.WriteAsJsonAsync(payload, JsonOptions, context.RequestAborted);
    }
}
