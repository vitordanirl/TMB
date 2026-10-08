using Orders.Infrastructure;
using Orders.Infrastructure.Health;
using Orders.Infrastructure.Messaging;
using Orders.Worker.Processing;

var builder = WebApplication.CreateBuilder(args);

var ordersConnectionString = builder.Configuration.GetRequiredConnectionString("Orders");
var rabbitMqConnectionString = builder.Configuration.GetRequiredConnectionString("RabbitMq");

builder.Services
    .AddOptions<OrderProcessingOptions>()
    .BindConfiguration(OrderProcessingOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOrdersPersistence(ordersConnectionString);
builder.Services.AddOrdersMessaging(rabbitMqConnectionString, bus =>
{
    bus.AddConsumer<OrderCreatedConsumer>()
        .Endpoint(endpoint => endpoint.Name = QueueNames.OrderCreated);

    bus.AddConsumer<CompleteOrderProcessingConsumer>()
        .Endpoint(endpoint => endpoint.Name = QueueNames.CompleteOrderProcessing);
});
builder.Services.AddOrdersHealthChecks(ordersConnectionString);

var app = builder.Build();

// O worker expõe apenas health checks (usados pela orquestração).
app.MapOrdersHealthChecks();

await app.RunAsync();
