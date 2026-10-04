using Microsoft.EntityFrameworkCore;
using StockSync.Inventory.Application.Productos;
using StockSync.Inventory.Application.Stocks;
using StockSync.Inventory.Domain.Repositories;
using StockSync.Inventory.Infrastructure;
using StockSync.Inventory.Infrastructure.Repositories;
using StockSync.Inventory.WebApi.ExceptionHandling;

var builder = WebApplication.CreateBuilder(args);

// Configurar DbContext con PostgreSQL
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IProductoRepository, ProductoRepository>();
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<IStockRepository, StockRepository>();
builder.Services.AddScoped<IStockService, StockService>();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// TODO para el equipo: Agregar los endpoints del Inventory Service aquí o usar Controllers.
app.MapControllers();

app.Run();
