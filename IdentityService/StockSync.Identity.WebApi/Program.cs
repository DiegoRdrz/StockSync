using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StockSync.Identity.Application.Auth;
using StockSync.Identity.Application.Common;
using StockSync.Identity.Application.Common.Interfaces;
using StockSync.Identity.Application.Tenants;
using StockSync.Identity.Application.Users;
using StockSync.Identity.Domain.Repositories;
using StockSync.Identity.Infrastructure;
using StockSync.Identity.Infrastructure.Repositories;
using StockSync.Identity.Infrastructure.Services;
using StockSync.Identity.WebApi.ExceptionHandling;
using StockSync.Identity.WebApi.MultiTenancy;

var executeSeed = args.Contains("--seed");
var executeMigrations = args.Contains("--migrate");
var builder = WebApplication.CreateBuilder(args.Where(arg => arg is not "--seed" and not "--migrate").ToArray());

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentTenant, CurrentTenantHttp>();
builder.Services.AddScoped<ICurrentUser, CurrentUserHttp>();

builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITenantService, TenantService>();

var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "StockSyncSuperSecretSecureKey123456789!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "StockSync.Identity";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "StockSync.Ecosystem";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = AutomaticValidation.CreateResponse);

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "StockSync Identity API",
        Version = "v1",
        Description = "Authentication, tenant management, and user identity service for StockSync SaaS."
    });

    options.OperationFilter<TenantHeaderOperationFilter>();

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT Bearer token like: Bearer {token}"
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

var applyMigrations = executeMigrations || app.Configuration.GetValue<bool>("Initialization:ApplyMigrations");
var loadDemoData = executeSeed || app.Configuration.GetValue<bool>("Initialization:LoadDemoData");

if (applyMigrations || loadDemoData)
{
    if (loadDemoData && !app.Environment.IsDevelopment())
        throw new InvalidOperationException("Demo data can only be seeded in Development environment.");

    await using var scope = app.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

    if (applyMigrations)
    {
        await context.Database.MigrateAsync();
        app.Logger.LogInformation("Database migrations applied successfully.");
    }

    if (loadDemoData)
    {
        await IdentityDbSeeder.SeedAsync(context, passwordHasher);
        app.Logger.LogInformation("Demo identity data seeded successfully.");
    }
}

if (executeSeed || executeMigrations)
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
