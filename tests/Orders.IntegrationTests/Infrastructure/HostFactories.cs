extern alias worker;

using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using WorkerProgram = worker::Program;

namespace Orders.IntegrationTests.Infrastructure;

/// <summary>API real (endpoints, Outbox, consumidor de notificações) apontando para os containers.</summary>
public sealed class OrdersApiFactory(IntegrationTestEnvironment environment) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Orders", environment.PostgresConnectionString);
        builder.UseSetting("ConnectionStrings:RabbitMq", environment.RabbitMqConnectionString);
    }
}

/// <summary>Worker real (consumidores com Outbox/Inbox) apontando para os containers.</summary>
public sealed class OrdersWorkerFactory(IntegrationTestEnvironment environment) : WebApplicationFactory<WorkerProgram>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Orders", environment.PostgresConnectionString);
        builder.UseSetting("ConnectionStrings:RabbitMq", environment.RabbitMqConnectionString);
        builder.UseSetting(
            "OrderProcessing:CompletionDelay",
            IntegrationTestEnvironment.CompletionDelay.ToString("c", CultureInfo.InvariantCulture));
    }
}
