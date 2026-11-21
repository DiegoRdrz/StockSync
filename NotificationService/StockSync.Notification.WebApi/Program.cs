using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StockSync.Notification.Application.Channels;
using StockSync.Notification.Application.Common;
using StockSync.Notification.Application.IntegrationEvents;
using StockSync.Notification.Application.Notifications;
using StockSync.Notification.Application.Recipients;
using StockSync.Notification.Domain.Repositories;
using StockSync.Notification.Infrastructure;
using StockSync.Notification.Infrastructure.Messaging;
using StockSync.Notification.Infrastructure.Repositories;
using StockSync.Notification.Infrastructure.Senders;
using StockSync.Notification.WebApi.ExceptionHandling;
using StockSync.Notification.WebApi.Multitenancy;

var runSeed = args.Contains("--seed");
var runMigrations = args.Contains("--migrate");
var builder = WebApplication.CreateBuilder(args.Where(arg => arg is not "--seed" and not "--migrate").ToArray());

builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<TenantContext>());

builder.Services.AddDbContext<NotificationDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
    .ConfigureWarnings(warnings => warnings.Log(
        (CoreEventId.SaveChangesFailed, LogLevel.Debug),
        (RelationalEventId.CommandError, LogLevel.Debug))));

builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<INotificationRecipientRepository, NotificationRecipientRepository>();
builder.Services.AddScoped<INotificationRecipientService, NotificationRecipientService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new SmtpOptions(
    builder.Configuration["Smtp:Host"],
    builder.Configuration.GetValue("Smtp:Port", 587),
    builder.Configuration["Smtp:FromAddress"] ?? "no-reply@stocksync.demo",
    builder.Configuration["Smtp:Username"],
    builder.Configuration["Smtp:Password"],
    builder.Configuration.GetValue("Smtp:EnableSsl", false)));
builder.Services.AddSingleton<INotificationChannelSender, EmailNotificationSender>();

builder.Services.AddSingleton(new WhatsAppOptions(builder.Configuration["WhatsApp:WebhookUrl"]));
builder.Services.AddHttpClient<WhatsAppNotificationSender>();
builder.Services.AddTransient<INotificationChannelSender>(sp => sp.GetRequiredService<WhatsAppNotificationSender>());

builder.Services.AddScoped<NotificationDispatcher>();
builder.Services.AddScoped<IEventProcessor<OrderCreatedEvent>, OrderCreatedEventProcessor>();
builder.Services.AddScoped<IEventProcessor<StockMovementRecordedEvent>, StockMovementEventProcessor>();
builder.Services.AddScoped(typeof(NotificationEventDelivery<>));

var rabbitMqConnection = builder.Configuration["RabbitMq:ConnectionString"];
if (!runSeed && !runMigrations && !string.IsNullOrWhiteSpace(rabbitMqConnection))
{
    var brokerUri = new Uri(rabbitMqConnection);
    RegisterConsumer<OrderCreatedEvent>(builder.Services, brokerUri);
    RegisterConsumer<StockMovementRecordedEvent>(builder.Services, brokerUri);
}

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
    .AddJsonOptions(options =>
    {
        options.AllowInputFormatterExceptionMessages = false;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
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

if (string.IsNullOrWhiteSpace(rabbitMqConnection))
    app.Logger.LogWarning("RabbitMq:ConnectionString is not configured; notification consumers are disabled.");

var applyMigrations = runMigrations || app.Configuration.GetValue<bool>("Initialization:ApplyMigrations");
var loadDemoData = runSeed || app.Configuration.GetValue<bool>("Initialization:LoadDemoData");

if (applyMigrations || loadDemoData)
{
    if (loadDemoData && !app.Environment.IsDevelopment())
        throw new InvalidOperationException("Demo data can only be loaded in Development.");

    await using var scope = app.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    if (applyMigrations)
    {
        await context.Database.MigrateAsync();
        app.Logger.LogInformation("Migrations completed.");
    }
    if (loadDemoData)
    {
        await NotificationDbSeeder.SeedAsync(context);
        app.Logger.LogInformation("Seeding completed. Check recipients at /api/notifications/recipients.");
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

static void RegisterConsumer<T>(IServiceCollection services, Uri broker)
    where T : class, INotificationEvent<T> =>
    services.AddHostedService(sp => new NotificationConsumer<T>(
        broker,
        sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<ILogger<NotificationConsumer<T>>>()));
