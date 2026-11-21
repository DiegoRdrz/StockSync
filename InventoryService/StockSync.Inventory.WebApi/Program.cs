using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StockSync.Inventory.Application.Categories;
using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Application.Movements;
using StockSync.Inventory.Application.Products;
using StockSync.Inventory.Application.Stocks;
using StockSync.Inventory.Domain.Repositories;
using StockSync.Inventory.Infrastructure;
using StockSync.Inventory.Infrastructure.Repositories;
using StockSync.Inventory.WebApi.ExceptionHandling;
using StockSync.Inventory.WebApi.Multitenancy;

var runSeed = args.Contains("--seed");
var runMigrations = args.Contains("--migrate");
var builder = WebApplication.CreateBuilder(args.Where(arg => arg is not "--seed" and not "--migrate").ToArray());

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

builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "StockSync.Identity";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "StockSync.Ecosystem";
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey configuration is missing.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
            RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers(AutomaticValidation.Configure)
    .AddJsonOptions(options => options.AllowInputFormatterExceptionMessages = false)
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = AutomaticValidation.CreateResponse);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<TenantHeaderOperationFilter>();
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// --migrate and --seed prepare the database and exit. Initialization settings in Docker
// prepare the database at startup and keep the API running.
var applyMigrations = runMigrations || app.Configuration.GetValue<bool>("Initialization:ApplyMigrations");
var loadDemoData = runSeed || app.Configuration.GetValue<bool>("Initialization:LoadDemoData");

if (applyMigrations || loadDemoData)
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
}

if (runSeed || runMigrations)
    return;

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
