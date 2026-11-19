FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["StockSync.Identity.WebApi/StockSync.Identity.WebApi.csproj", "StockSync.Identity.WebApi/"]
COPY ["StockSync.Identity.Infrastructure/StockSync.Identity.Infrastructure.csproj", "StockSync.Identity.Infrastructure/"]
COPY ["StockSync.Identity.Application/StockSync.Identity.Application.csproj", "StockSync.Identity.Application/"]
COPY ["StockSync.Identity.Domain/StockSync.Identity.Domain.csproj", "StockSync.Identity.Domain/"]
RUN dotnet restore "StockSync.Identity.WebApi/StockSync.Identity.WebApi.csproj"
COPY . .
WORKDIR "/src/StockSync.Identity.WebApi"
RUN dotnet build "StockSync.Identity.WebApi.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "StockSync.Identity.WebApi.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV Initialization__ApplyMigrations=true
ENTRYPOINT ["dotnet", "StockSync.Identity.WebApi.dll"]

