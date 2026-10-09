using System.Diagnostics;
using AwesomeAssertions;
using Orders.Infrastructure.Observability;

namespace Orders.UnitTests.Observability;

public sealed class SuppressOrphanDatabaseSpansProcessorTests : IDisposable
{
    private readonly ActivitySource _npgsql = new("Npgsql");
    private readonly ActivitySource _app = new("Orders.Tests");
    private readonly ActivityListener _listener = new()
    {
        ShouldListenTo = _ => true,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    };
    private readonly SuppressOrphanDatabaseSpansProcessor _processor = new();

    public SuppressOrphanDatabaseSpansProcessorTests()
    {
        ActivitySource.AddActivityListener(_listener);
    }

    [Fact]
    public void SpanDeBancoSemPai_DeveSerDescartado()
    {
        using var activity = _npgsql.StartActivity("postgresql SELECT")!;

        _processor.OnStart(activity);

        activity.Recorded.Should().BeFalse();
        activity.IsAllDataRequested.Should().BeFalse();
    }

    [Fact]
    public void SpanDeBancoDentroDeUmaOperacao_DeveSerMantido()
    {
        using var parent = _app.StartActivity("POST /orders")!;
        using var activity = _npgsql.StartActivity("postgresql INSERT")!;

        _processor.OnStart(activity);

        activity.Recorded.Should().BeTrue();
    }

    [Fact]
    public void SpanRaizDeOutraOrigem_DeveSerMantido()
    {
        using var activity = _app.StartActivity("POST /orders")!;

        _processor.OnStart(activity);

        activity.Recorded.Should().BeTrue();
    }

    public void Dispose()
    {
        _listener.Dispose();
        _npgsql.Dispose();
        _app.Dispose();
        _processor.Dispose();
    }
}
