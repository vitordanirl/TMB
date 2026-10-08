using System.Net;
using Microsoft.EntityFrameworkCore;
using Orders.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Orders.IntegrationTests.Infrastructure;

[CollectionDefinition(IntegrationTestEnvironment.Collection)]
public sealed class IntegrationTestEnvironmentDefinition : ICollectionFixture<IntegrationTestEnvironment>;

/// <summary>
/// Ambiente isolado e descartável: PostgreSQL e RabbitMQ reais em containers (Testcontainers),
/// com a API e o Worker rodando em processo contra eles. Criado uma vez por execução e destruído ao final.
/// </summary>
public sealed class IntegrationTestEnvironment : IAsyncLifetime
{
    /// <summary>Coleção xUnit que compartilha este ambiente (testes rodam em sequência).</summary>
    public const string Collection = "integration";

    /// <summary>Atraso Processando → Finalizado nos testes (5 s em produção).</summary>
    public static readonly TimeSpan CompletionDelay = TimeSpan.FromSeconds(1);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4.3-management-alpine").Build();

    public OrdersApiFactory Api { get; private set; } = null!;

    public OrdersWorkerFactory Worker { get; private set; } = null!;

    public string PostgresConnectionString => _postgres.GetConnectionString();

    public string RabbitMqConnectionString => _rabbitMq.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

        await using (var db = CreateDbContext())
        {
            await db.Database.MigrateAsync();
        }

        Api = new OrdersApiFactory(this);
        Worker = new OrdersWorkerFactory(this);

        // Só segue quando os dois hosts estão prontos (bus conectado ao RabbitMQ e banco acessível).
        await WaitUntilReadyAsync(Api.CreateClient());
        await WaitUntilReadyAsync(Worker.CreateClient());
    }

    public OrdersDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<OrdersDbContext>().UseNpgsql(PostgresConnectionString).Options);

    public async Task DisposeAsync()
    {
        await Worker.DisposeAsync();
        await Api.DisposeAsync();
        await _rabbitMq.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private static Task<HttpStatusCode> WaitUntilReadyAsync(HttpClient client) =>
        Eventually.Until(
            async () => (await client.GetAsync(new Uri("/health/ready", UriKind.Relative))).StatusCode,
            status => status == HttpStatusCode.OK,
            TimeSpan.FromSeconds(60));
}
