using StockSync.Reporting.Application;
using Microsoft.EntityFrameworkCore;
using StockSync.Reporting.Application.Common;
using StockSync.Reporting.Infrastructure;
using StockSync.Reporting.WebApi.ExceptionHandling;
using StockSync.Reporting.WebApi.Multitenancy;

var migrate = args.Contains("--migrate");
var builder = WebApplication.CreateBuilder(args.Where(a => a != "--migrate").ToArray());
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddDbContext<ReportingDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddControllers(AutomaticValidation.Configure)
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = AutomaticValidation.CreateResponse);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => o.OperationFilter<TenantHeaderOperationFilter>());
builder.Services.AddScoped<ISalesReports, SalesReports>();
builder.Services.AddScoped<IPurchaseReports, PurchaseReports>();
builder.Services.AddScoped<IInventoryReports, InventoryReports>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IProjection<SaleConfirmed>, SalesProjection>();
builder.Services.AddScoped<IProjection<PurchaseOrderReceived>, PurchaseProjection>();
builder.Services.AddScoped<IProjection<StockMovementRecorded>, StockMovementProjection>();
builder.Services.AddScoped(typeof(EventDelivery<>));
if (builder.Configuration["RabbitMq:ConnectionString"] is { Length: > 0 } broker)
{
    var uri = new Uri(broker);
    RegisterConsumer<SaleConfirmed>(builder.Services, uri);
    RegisterConsumer<PurchaseOrderReceived>(builder.Services, uri);
    RegisterConsumer<StockMovementRecorded>(builder.Services, uri);
}
var app = builder.Build();
if (migrate || app.Configuration.GetValue<bool>("Initialization:ApplyMigrations"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ReportingDbContext>().Database.MigrateAsync();
}
if (migrate) return;
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseExceptionHandler();
app.UseMiddleware<TenantResolutionMiddleware>();
app.MapControllers();
app.Run();

static void RegisterConsumer<T>(IServiceCollection services, Uri broker) where T : class, IReportingEvent<T> =>
    services.AddHostedService(sp => new ReportingConsumer<T>(broker, sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<ILogger<ReportingConsumer<T>>>()));
