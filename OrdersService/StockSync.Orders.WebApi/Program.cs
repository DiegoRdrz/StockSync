using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StockSync.Orders.Application.Common;
using StockSync.Orders.Application.IntegrationEvents;
using StockSync.Orders.Application.Inventory;
using StockSync.Orders.Application.Orders;
using StockSync.Orders.Application.PurchaseOrders;
using StockSync.Orders.Domain.Repositories;
using StockSync.Orders.Infrastructure;
using StockSync.Orders.Infrastructure.Inventory;
using StockSync.Orders.Infrastructure.Messaging;
using StockSync.Orders.Infrastructure.Outbox;
using StockSync.Orders.Infrastructure.Repositories;
using StockSync.Orders.WebApi.ExceptionHandling;
using StockSync.Orders.WebApi.Multitenancy;

var runMigrations = args.Contains("--migrate");
var builder = WebApplication.CreateBuilder(args.Where(arg => arg is not "--migrate").ToArray());

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentTenant, HttpCurrentTenant>();
builder.Services.AddDbContext<OrdersDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
    // Save failures mapped to 409 are not server errors; unexpected failures
    // are logged with their complete exception by ApiExceptionHandler.
    .ConfigureWarnings(warnings => warnings.Log(
        (CoreEventId.SaveChangesFailed, LogLevel.Debug),
        (RelationalEventId.CommandError, LogLevel.Debug))));

builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IIntegrationEventOutbox, EfIntegrationEventOutbox>();

// Inventory is called directly on the internal network. No automatic retries:
// stock movements are not idempotent (see StockSaga).
builder.Services.AddHttpClient<IInventoryClient, InventoryHttpClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Inventory:BaseUrl"]
        ?? throw new InvalidOperationException("Inventory:BaseUrl is not configured."));
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Inventory:TimeoutSeconds", 10));
});

// Without a broker, events stay in OutboxMessages and are published once RabbitMQ is configured.
var rabbitMqConnection = builder.Configuration["RabbitMq:ConnectionString"];
if (!string.IsNullOrWhiteSpace(rabbitMqConnection))
{
    builder.Services.AddSingleton(new RabbitMqOptions(
        rabbitMqConnection, builder.Configuration["RabbitMq:Exchange"] ?? "stocksync.events"));
    builder.Services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
    builder.Services.AddHostedService<OutboxPublisher>();
}

builder.Services.AddControllers(AutomaticValidation.Configure)
    .AddJsonOptions(options => options.AllowInputFormatterExceptionMessages = false)
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = AutomaticValidation.CreateResponse);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.OperationFilter<TenantHeaderOperationFilter>());

var app = builder.Build();

if (string.IsNullOrWhiteSpace(rabbitMqConnection))
    app.Logger.LogWarning("RabbitMq:ConnectionString is not configured; integration events remain in the outbox.");

// --migrate prepares the database and exits. Initialization:ApplyMigrations in Docker
// prepares the database at startup and keeps the API running.
if (runMigrations || app.Configuration.GetValue<bool>("Initialization:ApplyMigrations"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    await context.Database.MigrateAsync();
    app.Logger.LogInformation("Migrations completed.");
}

if (runMigrations)
    return;

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseMiddleware<TenantResolutionMiddleware>();
app.MapControllers();

app.Run();
