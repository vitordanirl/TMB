using MassTransit.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Orders.Infrastructure.Observability;

public static class ObservabilityExtensions
{
    /// <summary>
    /// Tracing distribuído com OpenTelemetry: HTTP (ASP.NET Core/HttpClient), MassTransit
    /// (publish/send/consume, inclusive via Outbox) e PostgreSQL (Npgsql). O contexto W3C viaja nos
    /// headers das mensagens, então um único trace cobre POST → Outbox → RabbitMQ → Worker → banco.
    /// </summary>
    /// <remarks>
    /// A exportação OTLP só é ativada quando <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> está definido
    /// (ex.: <c>http://jaeger:4317</c>), mantendo o desenvolvimento local e os testes independentes do coletor.
    /// Os logs passam a incluir TraceId/SpanId, permitindo ir do log ao trace.
    /// </remarks>
    public static IHostApplicationBuilder AddOrdersObservability(this IHostApplicationBuilder builder, string serviceName)
    {
        builder.Logging.Configure(options =>
            options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);
        builder.Logging.AddSimpleConsole(options =>
        {
            options.IncludeScopes = true;
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss.fff ";
        });

        var exportEnabled = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: typeof(ObservabilityExtensions).Assembly.GetName().Version?.ToString())
                .AddAttributes([new("deployment.environment.name", builder.Environment.EnvironmentName)]))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(DiagnosticHeaders.DefaultListenerName) // MassTransit
                    .AddAspNetCoreInstrumentation(options => options.Filter = IsTraceableRequest)
                    .AddHttpClientInstrumentation()
                    .AddNpgsql()
                    .AddProcessor(new SuppressOrphanDatabaseSpansProcessor());

                if (exportEnabled)
                {
                    tracing.AddOtlpExporter();
                }
            });

        return builder;
    }

    // Health checks rodam a cada poucos segundos (Docker/orquestrador) e só gerariam ruído.
    private static bool IsTraceableRequest(HttpContext context) =>
        !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
}
