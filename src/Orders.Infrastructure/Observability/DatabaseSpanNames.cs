using System.Buffers;
using Npgsql;

namespace Orders.Infrastructure.Observability;

/// <summary>
/// Nomeia os spans do Npgsql pela operação SQL (ex.: "postgresql SELECT") em vez do padrão
/// (apenas o nome do banco), deixando a linha do tempo do trace legível.
/// </summary>
internal static class DatabaseSpanNames
{
    private const string Prefix = "postgresql";

    // Fim da primeira palavra do SQL. Executado a cada comando, por isso o SearchValues em cache.
    private static readonly SearchValues<char> VerbDelimiters = SearchValues.Create(" \t\r\n(;");

    public static string? ForCommand(NpgsqlCommand command) => FromSql(command.CommandText);

    public static string? ForBatch(NpgsqlBatch batch) =>
        batch.BatchCommands.Count switch
        {
            0 => null,
            1 => FromSql(batch.BatchCommands[0].CommandText),
            var count => $"{Prefix} batch ({count})",
        };

    internal static string? FromSql(string? sql)
    {
        var text = sql.AsSpan().TrimStart();
        if (text.IsEmpty)
        {
            return null; // mantém o nome padrão
        }

        var end = text.IndexOfAny(VerbDelimiters);
        var verb = end < 0 ? text : text[..end];

        return $"{Prefix} {verb.ToString().ToUpperInvariant()}";
    }
}
