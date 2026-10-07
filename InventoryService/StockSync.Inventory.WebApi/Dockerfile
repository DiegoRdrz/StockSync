FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["StockSync.Inventory.WebApi/StockSync.Inventory.WebApi.csproj", "StockSync.Inventory.WebApi/"]
COPY ["StockSync.Inventory.Infrastructure/StockSync.Inventory.Infrastructure.csproj", "StockSync.Inventory.Infrastructure/"]
COPY ["StockSync.Inventory.Application/StockSync.Inventory.Application.csproj", "StockSync.Inventory.Application/"]
COPY ["StockSync.Inventory.Domain/StockSync.Inventory.Domain.csproj", "StockSync.Inventory.Domain/"]
RUN dotnet restore "StockSync.Inventory.WebApi/StockSync.Inventory.WebApi.csproj"
COPY . .
WORKDIR "/src/StockSync.Inventory.WebApi"
RUN dotnet build "StockSync.Inventory.WebApi.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "StockSync.Inventory.WebApi.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Al arrancar, la API aplica las migraciones pendientes para que la base quede completa.
# Para gestionarlas aparte (por ejemplo, con varias réplicas), definir Inicializacion__AplicarMigraciones=false.
ENV Inicializacion__AplicarMigraciones=true
ENTRYPOINT ["dotnet", "StockSync.Inventory.WebApi.dll"]
