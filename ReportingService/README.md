# ReportingService

Run `docker compose up -d --build reporting-api` from the repository root. Development Swagger is available on port 5004. For a local API, start reporting-db and rabbitmq, then run `dotnet run --project StockSync.Reporting.WebApi -- --migrate` and `dotnet run --project StockSync.Reporting.WebApi` from this directory. The local HTTP profile uses port 5046. Set `ConnectionStrings__DefaultConnection` and `RabbitMq__ConnectionString` to override the development connections.

## Delivery and error recovery

Start Reporting before publishing so its durable queues are bound to the topic exchange `stocksync.events`.

| Queue | Routing key | Error queue on `reporting.dlx` |
| --- | --- | --- |
| reporting.sales | orders.sale-confirmed.v1 | reporting.sales.error |
| reporting.purchases | orders.purchase-order-received.v1 | reporting.purchases.error |
| reporting.stock-movements | inventory.stock-movement-recorded.v1 | reporting.stock-movements.error |

Each queue has a dedicated consumer and channel. The consumer checks the routing key, message type, message ID and tenant header against the event body. Each delivery gets a fresh tenant scope. PostgreSQL commits the projection and its deduplication key together before acknowledgement. Invalid messages go directly to the queue's error queue. Processing failures get up to three attempts with exponential backoff. A consumer crash during retries causes broker redelivery with a fresh attempt count.

To reprocess an error in development, inspect the logged reason and correct the underlying problem. Pause competing error-queue readers. Read the intended error message with manual acknowledgement and republish its body to stocksync.events using the routing key for its source queue in the table above. Preserve its original event identity, content type, persistent delivery and tenant header, correcting invalid envelope fields against the source record. Confirm publication to the bound queue before acknowledging the error message. A repeated replay is safe because the consumer deduplicates by event ID.

## Report queries

Send a non-empty GUID in X-Tenant-Id on every GET request. Sales, purchase and movement reports accept inclusive UTC from and to and an optional branchId. Date-only input denotes midnight UTC. The default window ends now and starts 30 days before the selected end. The maximum range is 366 days.

| Route | Result |
| --- | --- |
| /api/reports/sales/summary | Total amount, count, average ticket and units |
| /api/reports/sales/daily | Amount, count and units by UTC day |
| /api/reports/sales/top-products | Products ranked by quantity, revenue and product ID. Limit defaults to 10 and accepts 1 through 50. |
| /api/reports/sales | Details ordered by confirmation time descending then order ID. Page starts at 1. Page size defaults to 20 and accepts 1 through 100. |
| /api/reports/purchases/summary | Total amount, count of received purchase orders and units received |
| /api/reports/purchases/daily | Amount, count and units received by UTC day of receipt |
| /api/reports/inventory/movements/summary | Counts, total moved quantity and signed net change grouped by movement type, UTC day and product ID |
| /api/reports/inventory/low-stock | Latest known levels below a positive minimum, ordered by shortage descending then stock ID. Accepts branchId, page and pageSize with the sales pagination defaults. |

Purchase receipts are informational. Inventory reporting uses all stock movements, including saga compensation. Moved quantity counts both directions as positive. Net change sums each movement's new balance minus its previous balance. Low stock uses the latest known balance for each tenant and stock across all dates. A later movement replaces the balance and its product metadata together. Equal timestamps use movementId as a deterministic tie-breaker, which may differ from actual write order.

## Verification

From ReportingService run `dotnet build StockSync.Reporting.sln` and `dotnet test StockSync.Reporting.sln`. Set STOCKSYNC_TEST_POSTGRES to an administrative PostgreSQL connection string with CREATE DATABASE permission for isolated database tests. Set STOCKSYNC_TEST_RABBITMQ to a development AMQP URI for the broker test. It creates isolated exchanges, queues and error queues and deletes them after the test. From the repository root run `docker compose config --quiet` and `docker compose build reporting-api`.
