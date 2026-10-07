using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StockSync.Inventory.Application.Categorias;
using StockSync.Inventory.Application.Common;
using StockSync.Inventory.Application.Movimientos;
using StockSync.Inventory.Application.Productos;
using StockSync.Inventory.Application.Stocks;
using StockSync.Inventory.Domain.Repositories;
using StockSync.Inventory.Infrastructure;
using StockSync.Inventory.Infrastructure.Repositories;
using StockSync.Inventory.WebApi.ExceptionHandling;
using StockSync.Inventory.WebApi.Multitenencia;

var ejecutarSemilla = args.Contains("--seed");
var ejecutarMigraciones = args.Contains("--migrate");
var builder = WebApplication.CreateBuilder(args.Where(arg => arg is not "--seed" and not "--migrate").ToArray());

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantActual, TenantActualHttp>();
builder.Services.AddDbContext<InventoryDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
    // Los fallos al guardar que se traducen a 409 no son errores del servidor; los inesperados ya los
    // registra ApiExceptionHandler con su excepción completa.
    .ConfigureWarnings(warnings => warnings.Log(
        (CoreEventId.SaveChangesFailed, LogLevel.Debug),
        (RelationalEventId.CommandError, LogLevel.Debug))));

builder.Services.AddScoped<IProductoRepository, ProductoRepository>();
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<IStockRepository, StockRepository>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IMovimientoStockRepository, MovimientoStockRepository>();
builder.Services.AddScoped<IMovimientoStockService, MovimientoStockService>();
builder.Services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();

builder.Services.AddScoped<ICategoriaRepository, CategoriaRepository>();
builder.Services.AddScoped<ICategoriaService, CategoriaService>();

builder.Services.AddControllers(ValidacionAutomatica.Configurar)
    .AddJsonOptions(options => options.AllowInputFormatterExceptionMessages = false)
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = ValidacionAutomatica.CrearRespuesta);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.OperationFilter<EncabezadoTenantOperationFilter>());

var app = builder.Build();

// --migrate y --seed preparan la base y terminan. Con Inicializacion:* (activado en la imagen de Docker)
// la base se prepara al arrancar y la API sigue en marcha.
var aplicarMigraciones = ejecutarMigraciones || app.Configuration.GetValue<bool>("Inicializacion:AplicarMigraciones");
var cargarDatosDemo = ejecutarSemilla || app.Configuration.GetValue<bool>("Inicializacion:CargarDatosDemo");

if (aplicarMigraciones || cargarDatosDemo)
{
    if (cargarDatosDemo && !app.Environment.IsDevelopment())
        throw new InvalidOperationException("Los datos de demostración solo se pueden cargar en Development.");

    await using var scope = app.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    if (aplicarMigraciones)
    {
        await context.Database.MigrateAsync();
        app.Logger.LogInformation("Migraciones completadas.");
    }
    if (cargarDatosDemo)
    {
        await InventoryDbSeeder.SeedAsync(context);
        app.Logger.LogInformation("Semilla completada. Consulte los IDs en /api/productos y /api/stock/sucursal/{SucursalId}.",
            "11111111-1111-1111-1111-111111111111");
    }
}

if (ejecutarSemilla || ejecutarMigraciones)
    return;

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseMiddleware<ResolucionTenantMiddleware>();
app.MapControllers();

app.Run();
