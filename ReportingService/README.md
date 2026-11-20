# ReportingService

ReportingService provides read-only sales, purchasing and inventory reports. Its RabbitMQ consumers write sales, purchases, movement history and latest stock levels to `stocksync_reporting`. The HTTP API reads these tenant-scoped projections with PostgreSQL queries. Reports are eventually consistent because events must first pass through the producer outbox, RabbitMQ and the Reporting consumer.

## Configuration and startup

| Setting | Development value or behavior |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string for `stocksync_reporting`. Local development uses port 5435. |
| `RabbitMq__ConnectionString` | AMQP URI. The local .NET profile connects to the Compose broker at `amqp://stocksync:stocksync@localhost:5672` by default. If unset, the API serves existing projections and consumes no events. |
| `Initialization__ApplyMigrations` | Compose sets it to `true` so the service migrates before accepting traffic or consuming events. |

From the repository root, start Reporting and its dependencies with Docker:

```sh
docker compose up -d --build reporting-api
```

Use port 5004 for the direct API and development Swagger. The full stack also exposes `/api/reports` through the gateway on port 9090. RabbitMQ management is available on port 15672 with the root Compose credentials.

For a local .NET run, start `reporting-db` and `rabbitmq` with `docker compose up -d reporting-db rabbitmq`. From `ReportingService`, apply migrations and start the HTTP profile:

```sh
dotnet run --project StockSync.Reporting.WebApi --launch-profile http -- --migrate
dotnet run --project StockSync.Reporting.WebApi --launch-profile http
```

The local HTTP profile uses port 5046. Set the PostgreSQL and RabbitMQ connection strings if they differ from the development settings. The `--migrate` command applies pending migrations and exits. For manual migrations in Docker, run `docker compose run --rm reporting-api --migrate` before starting API replicas with startup migrations disabled.

## Delivery and error recovery

Start Reporting before publishing so its durable queues are bound to the durable topic exchange `stocksync.events`. Each main queue dead-letters to the durable direct exchange `reporting.dlx` using its error queue name as the routing key. Each durable error queue is bound to that exact key.

| Queue | Routing key | Error queue on `reporting.dlx` |
| --- | --- | --- |
| reporting.sales | orders.sale-confirmed.v1 | reporting.sales.error |
| reporting.purchases | orders.purchase-order-received.v1 | reporting.purchases.error |
| reporting.stock-movements | inventory.stock-movement-recorded.v1 | reporting.stock-movements.error |

Each main queue has a dedicated consumer and channel with prefetch one and manual acknowledgements. The consumer checks the routing key, message type, message ID and tenant header against the event body. Each delivery gets a fresh tenant scope. PostgreSQL commits the projection and its deduplication key together before acknowledgement. Invalid messages go directly to the queue's error queue. Processing failures get up to three attempts with 100 ms and 200 ms delays, then go to the error queue. A consumer crash during retries causes broker redelivery with a fresh attempt count.

To reprocess an error in development, inspect the logged reason and correct the underlying problem. Pause competing error-queue readers. Read the intended error message with manual acknowledgement and republish its body to stocksync.events using the routing key for its source queue in the table above. Use the original event ID as `message_id`, the routing key as `type`, `application/json` as the content type and persistent delivery. Set the `tenant-id` header to the source event's tenant. Confirm publication to the bound queue before acknowledging the error message. A repeated replay is safe because the consumer deduplicates by event ID.

## Report queries

For direct API calls, send a non-empty GUID in `X-Tenant-Id`. The gateway sets this header from the JWT tenant claim or the configured development fallback. Sales, purchase and movement reports accept inclusive UTC from and to and an optional branchId. Date-only input denotes midnight UTC. The default window ends now and starts 30 days before the selected end. The maximum range is 366 days.

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

## Replay and backfill

For Orders, reconnect to the development database and clear `"ProcessedAt"` on the intended `"OutboxMessages"` rows by event ID and tenant. The running publisher resends those stored events with their original IDs. See [the replay SQL and event contracts](../docs/integration-events.md#manual-orders-replay-in-development).

For Inventory, apply its migrations and enqueue existing movements across all tenants:

```sh
docker compose run --rm inventory-api --backfill-events
```

For local development, run `dotnet run --project StockSync.Inventory.WebApi -- --backfill-events` from `InventoryService`. Backfill preserves the original movement ID, timestamp and balances, and skips existing outbox IDs. The running Inventory publisher sends newly enqueued rows. Movements with unresolved product metadata are logged and skipped.

## Known limitations

- A stock assignment with zero initial quantity produces no movement and stays absent from low-stock until its first movement.
- A product name or minimum-stock change reaches Reporting with the next movement carrying that metadata.
- Inventory reports include saga compensation movements as receipts or issues.
- Inventory and Reporting trust the JWT tenant claim at the development gateway. The gateway does not verify the JWT signature. Orders uses a fixed development tenant.
- Backfill uses current product metadata for historical movements.

## Verification

From ReportingService run `dotnet build StockSync.Reporting.sln` and `dotnet test StockSync.Reporting.sln`. Set STOCKSYNC_TEST_POSTGRES to an administrative PostgreSQL connection string with CREATE DATABASE permission for isolated database tests. Set STOCKSYNC_TEST_RABBITMQ to a development AMQP URI for the broker tests. They create isolated exchanges, queues and error queues and delete them after each test. From the repository root run `docker compose config --quiet` and `docker compose build reporting-api`.
