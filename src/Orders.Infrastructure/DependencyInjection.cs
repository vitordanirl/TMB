using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orders.Infrastructure.Observability;
using Orders.Infrastructure.Persistence;

namespace Orders.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrdersPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<OrdersDbContext>(options => options
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history");
                npgsql.EnableRetryOnFailure();
                npgsql.ConfigureDataSource(dataSource => dataSource.ConfigureTracing(tracing => tracing
                    .ConfigureCommandSpanNameProvider(DatabaseSpanNames.ForCommand)
                    .ConfigureBatchSpanNameProvider(DatabaseSpanNames.ForBatch)));
            }));

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
