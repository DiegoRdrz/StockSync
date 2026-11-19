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
