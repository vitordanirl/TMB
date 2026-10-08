using System.Text.Json;
using System.Text.Json.Serialization;
using Orders.Api.ErrorHandling;
using Orders.Api.Features.Orders;
using Orders.Infrastructure;
using Orders.Infrastructure.Health;
using Orders.Infrastructure.Messaging;
using Scalar.AspNetCore;

const string CorsPolicy = "frontend";

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetRequiredConnectionString("Orders");
var rabbitMqConnectionString = builder.Configuration.GetRequiredConnectionString("RabbitMq");

builder.Services.AddOrdersPersistence(connectionString);
builder.Services.AddOrdersMessaging(rabbitMqConnectionString);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddValidation();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsConventions.Apply);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddOrdersHealthChecks(connectionString);

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

// Documentação habilitada em todos os ambientes por se tratar de um desafio técnico.
app.MapOpenApi();
app.MapScalarApiReference("/docs", options => options.WithTitle("Orders API"));

app.MapOrdersHealthChecks();
app.MapOrderEndpoints();

await app.RunAsync();
