using System.Diagnostics;
using OpenTelemetry;

namespace Orders.Infrastructure.Observability;

/// <summary>
/// Descarta spans de banco "órfãos" (sem span pai), como o polling do Outbox a cada segundo e os
/// health checks. Comandos SQL executados dentro de uma requisição ou de um consumo de mensagem
/// continuam sendo registrados normalmente, como filhos desses spans.
/// </summary>
internal sealed class SuppressOrphanDatabaseSpansProcessor : BaseProcessor<Activity>
{
    private const string NpgsqlSourceName = "Npgsql";

    public override void OnStart(Activity data)
    {
        if (data.Source.Name == NpgsqlSourceName && data.ParentSpanId == default)
        {
            data.IsAllDataRequested = false;
            data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        }
    }
}
