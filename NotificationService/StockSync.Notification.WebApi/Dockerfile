FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["StockSync.Notification.WebApi/StockSync.Notification.WebApi.csproj", "StockSync.Notification.WebApi/"]
COPY ["StockSync.Notification.Infrastructure/StockSync.Notification.Infrastructure.csproj", "StockSync.Notification.Infrastructure/"]
COPY ["StockSync.Notification.Application/StockSync.Notification.Application.csproj", "StockSync.Notification.Application/"]
COPY ["StockSync.Notification.Domain/StockSync.Notification.Domain.csproj", "StockSync.Notification.Domain/"]
RUN dotnet restore "StockSync.Notification.WebApi/StockSync.Notification.WebApi.csproj"
COPY . .
WORKDIR "/src/StockSync.Notification.WebApi"
RUN dotnet build "StockSync.Notification.WebApi.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "StockSync.Notification.WebApi.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV Initialization__ApplyMigrations=true
ENTRYPOINT ["dotnet", "StockSync.Notification.WebApi.dll"]
