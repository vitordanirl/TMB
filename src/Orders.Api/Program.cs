using System.Text.Json;
using System.Text.Json.Serialization;
using Orders.Api.ErrorHandling;
using Orders.Api.Features.Ai;
using Orders.Api.Features.Orders;
using Orders.Api.Realtime;
using Orders.Infrastructure;
using Orders.Infrastructure.Health;
using Orders.Infrastructure.Messaging;
using Orders.Infrastructure.Observability;
using Scalar.AspNetCore;

const string CorsPolicy = "frontend";

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetRequiredConnectionString("Orders");
var rabbitMqConnectionString = builder.Configuration.GetRequiredConnectionString("RabbitMq");

builder.AddOrdersObservability("orders-api");
builder.Services.AddOrdersPersistence(connectionString);
builder.Services.AddOrdersMessaging(rabbitMqConnectionString, bus =>
    bus.AddConsumer<OrderNotificationsConsumer>(typeof(OrderNotificationsConsumerDefinition)));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Por padrão, só em Development os minimal APIs lançam BadHttpRequestException para corpo/parâmetros
// malformados; nos demais ambientes respondem um 400 genérico. Lançando sempre, o ApiExceptionHandler
// produz o mesmo ProblemDetails em qualquer ambiente.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddValidation();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsConventions.Apply);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddOrdersHealthChecks(connectionString);
builder.Services.AddOrdersAssistant(builder.Configuration);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(CorsPolicy);
app.UseRateLimiter();

// Documentação habilitada em todos os ambientes por se tratar de um desafio técnico.
app.MapOpenApi();
app.MapScalarApiReference("/docs", options => options.WithTitle("Orders API"));

app.MapOrdersHealthChecks();
app.MapOrderEndpoints();
app.MapAiEndpoints();
app.MapHub<OrdersHub>(OrdersHub.Path);

await app.RunAsync();
