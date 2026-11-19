# ReportingService

Run `docker compose up -d --build reporting-api` from the repository root. Development Swagger is available on port 5004. For a local API, start reporting-db and rabbitmq, then run `dotnet run --project StockSync.Reporting.WebApi -- --migrate` and `dotnet run --project StockSync.Reporting.WebApi` from this directory. The local HTTP profile uses port 5046. Set `ConnectionStrings__DefaultConnection` and `RabbitMq__ConnectionString` to override the development connections.
