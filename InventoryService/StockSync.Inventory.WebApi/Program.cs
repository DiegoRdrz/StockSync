using Microsoft.EntityFrameworkCore;
using StockSync.Inventory.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Configurar DbContext con PostgreSQL
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// TODO para el equipo: Agregar los endpoints del Inventory Service aquí o usar Controllers.

app.Run();
