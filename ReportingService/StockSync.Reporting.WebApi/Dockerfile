FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["StockSync.Reporting.WebApi/StockSync.Reporting.WebApi.csproj", "StockSync.Reporting.WebApi/"]
COPY ["StockSync.Reporting.Infrastructure/StockSync.Reporting.Infrastructure.csproj", "StockSync.Reporting.Infrastructure/"]
COPY ["StockSync.Reporting.Application/StockSync.Reporting.Application.csproj", "StockSync.Reporting.Application/"]
COPY ["StockSync.Reporting.Domain/StockSync.Reporting.Domain.csproj", "StockSync.Reporting.Domain/"]
RUN dotnet restore "StockSync.Reporting.WebApi/StockSync.Reporting.WebApi.csproj"
COPY . .
WORKDIR "/src/StockSync.Reporting.WebApi"
RUN dotnet build "StockSync.Reporting.WebApi.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "StockSync.Reporting.WebApi.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Apply pending migrations before serving requests.
# Set Initialization__ApplyMigrations=false to manage migrations separately.
ENV Initialization__ApplyMigrations=true
ENTRYPOINT ["dotnet", "StockSync.Reporting.WebApi.dll"]
