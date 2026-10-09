namespace Orders.Api.Features.Ai;

/// <summary>
/// "Hoje", "este mês" etc. são interpretados no fuso do negócio (America/Sao_Paulo), não em UTC:
/// um pedido criado às 22h de 31/10 (horário de Brasília) é de outubro, embora já seja 01/11 em UTC.
/// </summary>
public sealed class BusinessClock(TimeProvider timeProvider)
{
    public const string TimeZoneId = "America/Sao_Paulo";

    // O Brasil não tem horário de verão desde 2019; o offset fixo cobre ambientes sem base tz.
    private static readonly TimeZoneInfo Zone = FindZone();

    public TimeZoneInfo TimeZone => Zone;

    public DateTimeOffset Now => TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Zone);

    public DateOnly Today => DateOnly.FromDateTime(Now.DateTime);

    /// <summary>Converte um período local inclusivo em um intervalo UTC semiaberto [início, fim).</summary>
    public (DateTimeOffset From, DateTimeOffset To) ToUtcRange(Period period)
    {
        var from = period.From is { } start ? StartOfDayUtc(start) : DateTimeOffset.MinValue.ToUniversalTime();
        var to = period.To is { } end ? StartOfDayUtc(end.AddDays(1)) : DateTimeOffset.MaxValue.ToUniversalTime();

        return (from, to);
    }

    private static DateTimeOffset StartOfDayUtc(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local)).ToUniversalTime();
    }

    private static TimeZoneInfo FindZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(TimeZoneId, TimeSpan.FromHours(-3), TimeZoneId, TimeZoneId);
        }
    }
}
