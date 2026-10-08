using AwesomeAssertions;
using Orders.Infrastructure.Observability;

namespace Orders.UnitTests.Observability;

public sealed class DatabaseSpanNamesTests
{
    [Theory]
    [InlineData("SELECT o.id FROM orders AS o", "postgresql SELECT")]
    [InlineData("  \n insert into orders (id) values (@p0)", "postgresql INSERT")]
    [InlineData("UPDATE orders SET status = @p0", "postgresql UPDATE")]
    [InlineData("DELETE FROM outbox_message WHERE ...", "postgresql DELETE")]
    [InlineData("SELECT;", "postgresql SELECT")]
    [InlineData("WITH cte AS (SELECT 1) SELECT * FROM cte", "postgresql WITH")]
    public void FromSql_DeveUsarAOperacaoSql(string sql, string expected)
    {
        DatabaseSpanNames.FromSql(sql).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromSql_SemTexto_DeveManterNomePadrao(string? sql)
    {
        DatabaseSpanNames.FromSql(sql).Should().BeNull();
    }
}
