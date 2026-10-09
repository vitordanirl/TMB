using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using Orders.Api.Features.Ai;

namespace Orders.IntegrationTests;

/// <summary>Interpretação de datas no fuso do negócio (não precisa dos containers).</summary>
public sealed class BusinessClockTests
{
    [Fact]
    public void Periodo_DeveSerConvertidoParaUtcNoHorarioDeBrasilia()
    {
        var clock = new BusinessClock(TimeProvider.System);

        var (from, to) = clock.ToUtcRange(new Period(new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 31)));

        from.Should().Be(new DateTimeOffset(2026, 10, 31, 3, 0, 0, TimeSpan.Zero));
        to.Should().Be(new DateTimeOffset(2026, 11, 1, 3, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Hoje_DeveConsiderarOFusoDoNegocio()
    {
        // 01/11 02:00 UTC ainda é 31/10 23:00 em Brasília.
        var clock = new BusinessClock(new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 2, 0, 0, TimeSpan.Zero)));

        clock.Today.Should().Be(new DateOnly(2026, 10, 31));
    }
}
