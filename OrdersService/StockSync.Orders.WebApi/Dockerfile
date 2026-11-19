FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["StockSync.Orders.WebApi/StockSync.Orders.WebApi.csproj", "StockSync.Orders.WebApi/"]
COPY ["StockSync.Orders.Infrastructure/StockSync.Orders.Infrastructure.csproj", "StockSync.Orders.Infrastructure/"]
COPY ["StockSync.Orders.Application/StockSync.Orders.Application.csproj", "StockSync.Orders.Application/"]
COPY ["StockSync.Orders.Domain/StockSync.Orders.Domain.csproj", "StockSync.Orders.Domain/"]
RUN dotnet restore "StockSync.Orders.WebApi/StockSync.Orders.WebApi.csproj"
COPY . .
WORKDIR "/src/StockSync.Orders.WebApi"
RUN dotnet build "StockSync.Orders.WebApi.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "StockSync.Orders.WebApi.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Apply pending migrations before serving requests.
# Set Initialization__ApplyMigrations=false to manage migrations separately.
ENV Initialization__ApplyMigrations=true
ENTRYPOINT ["dotnet", "StockSync.Orders.WebApi.dll"]
