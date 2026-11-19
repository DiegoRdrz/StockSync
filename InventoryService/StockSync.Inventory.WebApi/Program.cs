using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StockSync.Inventory.Application.Categories;
using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Application.IntegrationEvents;
using StockSync.Inventory.Application.Movements;
using StockSync.Inventory.Application.Products;
using StockSync.Inventory.Application.Stocks;
using StockSync.Inventory.Domain.Repositories;
using StockSync.Inventory.Infrastructure;
using StockSync.Inventory.Infrastructure.Messaging;
using StockSync.Inventory.Infrastructure.Outbox;
using StockSync.Inventory.Infrastructure.Repositories;
using StockSync.Inventory.WebApi.ExceptionHandling;
using StockSync.Inventory.WebApi.Multitenancy;

var runSeed = args.Contains("--seed");
var runMigrations = args.Contains("--migrate");
var runBackfill = args.Contains("--backfill-events");
var builder = WebApplication.CreateBuilder(args.Where(arg => arg is not "--seed" and not "--migrate" and not "--backfill-events").ToArray());

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentTenant, HttpCurrentTenant>();
builder.Services.AddDbContext<InventoryDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
    // Save failures mapped to 409 are not server errors; unexpected failures
    // are logged with their complete exception by ApiExceptionHandler.
    .ConfigureWarnings(warnings => warnings.Log(
        (CoreEventId.SaveChangesFailed, LogLevel.Debug),
        (RelationalEventId.CommandError, LogLevel.Debug))));

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IStockRepository, StockRepository>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IStockMovementRepository, StockMovementRepository>();
builder.Services.AddScoped<IStockMovementService, StockMovementService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IIntegrationEventOutbox, EfIntegrationEventOutbox>();
builder.Services.AddScoped<StockMovementEventBackfill>();

var rabbitMqConnection = builder.Configuration["RabbitMq:ConnectionString"];
if (!string.IsNullOrWhiteSpace(rabbitMqConnection))
{
    builder.Services.AddSingleton(new RabbitMqOptions(
        rabbitMqConnection, builder.Configuration["RabbitMq:Exchange"] ?? "stocksync.events"));
    builder.Services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
    builder.Services.AddHostedService<OutboxPublisher>();
}

builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

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

// Command flags prepare the database and exit. Initialization settings continue into the API.
var applyMigrations = runMigrations || app.Configuration.GetValue<bool>("Initialization:ApplyMigrations");
var loadDemoData = runSeed || app.Configuration.GetValue<bool>("Initialization:LoadDemoData");

if (applyMigrations || loadDemoData || runBackfill)
{
    if (loadDemoData && !app.Environment.IsDevelopment())
        throw new InvalidOperationException("Demo data can only be loaded in Development.");

    await using var scope = app.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    if (applyMigrations)
    {
        await context.Database.MigrateAsync();
        app.Logger.LogInformation("Migrations completed.");
    }
    if (loadDemoData)
    {
        await InventoryDbSeeder.SeedAsync(context);
        app.Logger.LogInformation("Seeding completed. Check IDs at /api/products and /api/stock/branch/{BranchId}.",
            "11111111-1111-1111-1111-111111111111");
    }
    if (runBackfill || loadDemoData)
        await scope.ServiceProvider.GetRequiredService<StockMovementEventBackfill>().RunAsync();
}

if (runSeed || runMigrations || runBackfill)
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
