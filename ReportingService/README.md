# ReportingService

Run `docker compose up -d --build reporting-api` from the repository root. Development Swagger is available on port 5004. For a local API, start reporting-db and rabbitmq, then run `dotnet run --project StockSync.Reporting.WebApi -- --migrate` and `dotnet run --project StockSync.Reporting.WebApi` from this directory. The local HTTP profile uses port 5046. Set `ConnectionStrings__DefaultConnection` and `RabbitMq__ConnectionString` to override the development connections.

## Sales delivery and error recovery

Bind the durable reporting.sales queue before publishing sales. The consumer checks the routing key, message type, message ID and tenant header against the sale contract. Each delivery gets a fresh tenant scope. PostgreSQL commits the sale, lines and deduplication key together before acknowledgement. Invalid messages go directly to reporting.sales.error. Processing failures get up to three attempts with exponential backoff. A consumer crash during retries causes broker redelivery with a fresh attempt count.

To reprocess an error in development, inspect the logged reason and correct the underlying problem. Pause competing error-queue readers. Read the intended error message with manual acknowledgement and republish its body to stocksync.events using orders.sale-confirmed.v1. Preserve its original event identity, content type, persistent delivery and tenant header, correcting invalid envelope fields against the source record. Confirm publication to the bound queue before acknowledging the error message. A repeated replay is safe because the consumer deduplicates by event ID.

## Query sales

Send a non-empty GUID in X-Tenant-Id on every GET request. All four routes accept inclusive UTC from and to and an optional branchId. Date-only input denotes midnight UTC. The default window ends now and starts 30 days before the selected end. The maximum range is 366 days.

| Route | Result |
| --- | --- |
| /api/reports/sales/summary | Total amount, count, average ticket and units |
| /api/reports/sales/daily | Amount, count and units by UTC day |
| /api/reports/sales/top-products | Products ranked by quantity, revenue and product ID. Limit defaults to 10 and accepts 1 through 50. |
| /api/reports/sales | Details ordered by confirmation time descending then order ID. Page starts at 1. Page size defaults to 20 and accepts 1 through 100. |
