# StockSync

Inventory and Orders APIs built with .NET 8 and PostgreSQL 15, exposed through a TypeScript/NestJS API gateway in `ApiGateway/`. Orders publishes integration events to RabbitMQ.

## Run with Docker

Start Docker Desktop, then run from the repository root:

```sh
cp -n .env.example .env
docker compose up -d --build
```

Open **http://localhost:9090/swagger**. The gateway forwards requests to `http://inventory-api:8080`. The inventory API remains directly accessible at http://localhost:5001 for development.

The API waits for PostgreSQL, applies pending migrations, and loads demo data before serving requests. Repeated startup does not reset stock balances. Configure `POSTGRES_PASSWORD`, `LOAD_DEMO_DATA`, `GATEWAY_PORT` and `GATEWAY_TENANT_ID` in the root `.env`.

```sh
docker compose logs -f inventory-api  # Ctrl+C exits the log viewer.
docker compose stop                 # Stop services and preserve data.
docker compose up -d --build         # Rebuild after source changes.
```

`docker compose down -v` also deletes the database volume. Use it only when intentionally resetting local data.

The image enables `Initialization__ApplyMigrations=true`. With multiple API replicas, disable startup migrations and run `docker compose run --rm inventory-api --migrate` once before starting the replicas. Demo data is controlled by `Initialization__LoadDemoData` and is allowed only in Development.

![Project architecture](Assets/Diagram.png)

The diagram includes planned services; this repository currently implements inventory, orders and the gateway. See [Orders service](#orders-service).

## Run locally

Install an SDK with the .NET 8 runtime and start PostgreSQL on `localhost:5432`:

```sh
docker compose up -d inventory-db
cd InventoryService
dotnet tool restore
dotnet restore
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update \
  --project StockSync.Inventory.Infrastructure \
  --startup-project StockSync.Inventory.WebApi

dotnet run --project StockSync.Inventory.WebApi --launch-profile http -- --seed
dotnet run --project StockSync.Inventory.WebApi --launch-profile http
```

Local Swagger is at http://localhost:5041/swagger. Docker exposes the direct API at port `5001` instead. Local migrations and seeding are explicit commands; ordinary `dotnet run` does not perform them automatically. Connection strings are configured through `ConnectionStrings__DefaultConnection` or development appsettings.

If macOS selects a .NET installation without the .NET 8 runtime, select the installation that contains it:

```sh
export DOTNET_ROOT=/usr/local/share/dotnet
export PATH="$DOTNET_ROOT:$PATH"
dotnet --list-runtimes
```

See [the gateway README](ApiGateway/README.md) for running the gateway with Node.js 24.

## Public routes

The gateway maps `/api/inventory/...` to `/api/...` on the inventory service.

| Gateway route | Purpose |
| --- | --- |
| `/api/inventory/products` | Product creation, updates, listing and soft deletion |
| `/api/inventory/categories` | Categories and category products |
| `/api/inventory/stock` | Product/branch assignments and quantity adjustments |
| `/api/inventory/stock/branch/{branchId}` | Stock assigned to a branch |
| `/api/inventory/stock/product/{productId}` | Stock assigned to a product |
| `/api/inventory/stock/low-stock?branchId=...` | Active-product stock below its minimum |
| `/api/inventory/stock/{stockId}/receipts` | Receive stock |
| `/api/inventory/stock/{stockId}/issues` | Issue stock |
| `/api/inventory/stock/{stockId}/movements` | Stock movement history |
| `/api/inventory/movements` | Latest 10 movements across all stocks |
| `/api/inventory/movements/page/{page}` | Subsequent pages of 10 movements |

Product requests use `name`, `sku`, `description`, `purchasePrice`, `salePrice`, `minimumStock` and `categoryId`. Stock creation uses `productId`, `branchId` and `quantity`; a manual update uses `newQuantity`. Receipt and issue requests use `quantity`.

List responses contain `items`, `page`, `pageSize`, `total` and `totalPages`. Lists accept `page` (at least 1) and `pageSize` (1–100, default 20), except the general movement list, which always uses pages of 10. Empty pages return 200 with an empty `items` array.

The general movement list is ordered by descending date and ID. Each item contains `productName`, `productSku`, `productId`, `stockId`, `branchId`, `type`, `quantity`, `previousQuantity`, `newQuantity` and `date`. Names and SKUs reflect current product data; inactive products remain in history.

## Demo data

`InventoryDbSeeder` creates two categories, three products, three stock assignments and five movements through Entity Framework and domain methods. A single transaction saves them in the default tenant. Seeding looks up active categories by normalized name, products by SKU (preferring an active match), and stock by product/branch. It does not reactivate products, reset balances or duplicate movements for existing assignments.

| SKU | Product | Balance | Example |
| --- | --- | --- | --- |
| `DEMO-HER-001` | Demo hammer | 7 | Receipts, issues and history |
| `DEMO-HER-002` | Demo drill | 0 | Issue rejected with HTTP 409 |
| `DEMO-PAP-001` | Demo notebook | 12 | Initial balance recorded as an adjustment |

The first two assignments use branch `11111111-1111-1111-1111-111111111111`; the third uses `22222222-2222-2222-2222-222222222222`. Branches belong to another service and are not modeled as inventory tables. SKU identifiers remain stable across the language migration.

```sh
curl http://localhost:9090/api/inventory/products
curl http://localhost:9090/api/inventory/categories
curl http://localhost:9090/api/inventory/stock/branch/11111111-1111-1111-1111-111111111111

# Replace with an actual stock ID.
stock_id='STOCK_ID'
curl "http://localhost:9090/api/inventory/stock/$stock_id/movements"
curl -i -X POST "http://localhost:9090/api/inventory/stock/$stock_id/issues" \
  -H 'Content-Type: application/json' -d '{"quantity":1}'
```

The [HTTP examples](InventoryService/StockSync.Inventory.WebApi/StockSync.Inventory.WebApi.http) cover the complete flow against the direct local API.

## Inventory rules

- Categories and products use soft deletion (`isActive = false`). Category names and SKUs are unique among active records within a tenant and can be reused after deactivation. History references product IDs, not SKUs.
- Movement types are `Receipt`, `Issue` and `Adjustment`. Initial balances and manual quantity changes create adjustments. An unchanged balance creates no movement.
- Issues exceeding the available balance return 409. Row locks serialize concurrent operations on the same stock; optimistic concurrency also protects stale writes.
- Categories with active products, products with remaining stock, and stock assignments with units or history cannot be deleted. Inactive products cannot receive movements or adjustments.
- Low-stock results contain active products with `quantity < minimumStock`, ordered by descending `shortage`. A zero minimum never produces an alert.

| Status | Meaning |
| --- | --- |
| 400 | Invalid input, including a missing or inactive product/category referenced in a body |
| 404 | A URL resource does not exist or is inactive |
| 409 | Duplicate data, insufficient stock, or an operation forbidden by the current state |

## Multitenancy

Categories, products, stock and movements belong to a tenant identified by the `X-Tenant-Id` GUID header. Tenants cannot read or reference each other's records. The direct API requires the header unless `Multitenancy:RequireHeader` is false, as configured in Development. When optional, omission uses `00000000-0000-0000-0000-000000000001`.

The gateway currently uses a fixed development tenant and replaces client-supplied tenant headers. It does not implement user authentication or per-user tenant resolution. A production gateway must authenticate users, resolve their authorized tenant, and restrict direct access to the backend.

## English migration

This branch changes public routes, JSON fields, query parameters, movement values, configuration keys, source identifiers and documentation to English. Existing clients must adopt the contracts above. Rename the root environment setting to `LOAD_DEMO_DATA` when upgrading an existing `.env`.

`TranslateSchemaToEnglish` renames tables, columns, indexes and constraints in place and converts stored movement types. It preserves IDs, tenant ownership, quantities and history, with a reverse migration available. Stop older API instances before applying it because the schema and contracts change together.

Historical migrations and their old database names are intentionally retained so existing migration histories remain valid. Legacy names also appear in the upgrade/rollback migration because those are the objects it must rename. Existing user-entered product names and descriptions are data and are not automatically translated.

## Orders service

`OrdersService/` manages sales orders and supplier purchase orders with its own database (`stocksync_orders`). In Docker the direct API is http://localhost:5003/swagger; locally it runs on http://localhost:5045 with `dotnet run --project StockSync.Orders.WebApi --launch-profile http` from `OrdersService` (start `orders-db`, `rabbitmq` and InventoryService first, then run `dotnet ef database update --project StockSync.Orders.Infrastructure --startup-project StockSync.Orders.WebApi` with `ASPNETCORE_ENVIRONMENT=Development`). The gateway forwards `/api/orders` and `/api/purchase-orders` unchanged. RabbitMQ management is at http://localhost:15672 (`RABBITMQ_USER` / `RABBITMQ_PASSWORD`, default `stocksync`).

| Route | Purpose |
| --- | --- |
| `POST /api/orders` | Create a draft sale for a branch (`branchId`, `notes`) |
| `GET /api/orders/{id}`, `GET /api/orders` | Get one order; list with `status`, `branchId`, `from`, `to`, `page`, `pageSize` |
| `POST /api/orders/{id}/lines` | Add a product (`productId`, `quantity`); the price is the Inventory `salePrice` |
| `PUT` / `DELETE /api/orders/{id}/lines/{lineId}` | Change the quantity or remove a line while in draft |
| `POST /api/orders/{id}/confirm` | Issue stock in Inventory and confirm the sale |
| `POST /api/orders/{id}/cancel` | Cancel a draft sale |
| `/api/purchase-orders/...` | Same operations with `supplierName`; prices use the Inventory `purchasePrice`; `place` sends the order and `receive` records stock receipts |

Order statuses are `Draft`, `Confirming`, `Confirmed`, `RequiresReview` and `Cancelled`. Purchase order statuses are `Draft`, `Ordered`, `Receiving`, `Received`, `RequiresReview` and `Cancelled`. Lines can change only in draft; a product appears once per document; quantities and prices must be greater than zero; totals are calculated by the domain. Confirmed sales cannot be cancelled (returns are not implemented).

### Stock consistency

Inventory has no batch or idempotent movement endpoint, so confirmation is an orchestrated saga with compensation and no changes to Inventory:

1. The order is locked and moved to `Confirming` in its own transaction, which blocks edits and a second confirmation.
2. Orders calls `POST /api/stock/{stockId}/issues` for each line and saves the returned movement ID after every call.
3. If Inventory refuses a line (for example insufficient stock, 409), the issued lines are reversed with `receipts`, the order returns to `Draft` and the API answers 409. If Inventory cannot be reached before anything is issued, the order returns to `Draft` with 503.
4. If a result is unknown (timeout, 5xx or a lost connection after sending), Orders never retries, because a retry could issue stock twice. The order moves to `RequiresReview` with the reason, the API answers 503, and a person reconciles it with the movement IDs stored on each line and the Inventory movement history.

Receiving a purchase order uses the same saga with receipts, compensated with issues. Calls go directly to `http://inventory-api:8080` with the caller's `X-Tenant-Id`; the request is not cancelled halfway even if the client disconnects.

### Integration events

`OrderCreated` and `SaleConfirmed` are written to `OutboxMessages` in the same transaction as the order change (transactional outbox) and published by a background worker to the durable topic exchange `stocksync.events` with routing keys `orders.order-created.v1` and `orders.sale-confirmed.v1`. Each message has the event ID as `message_id`, the event type as `type` and a `tenant-id` header; the JSON body uses camelCase. Delivery is at least once, so consumers must deduplicate by event ID and declare their own queues. `SaleConfirmed` is informational: Inventory has already issued the stock, so consuming it to decrement stock again would double the decrement. Without `RabbitMq:ConnectionString`, events stay in the outbox.

## Inventory service checks

From `InventoryService`:

```sh
dotnet test StockSync.Inventory.sln

# Requires a PostgreSQL role with CREATE DATABASE permission.
STOCKSYNC_TEST_POSTGRES='Host=localhost;Database=postgres;Username=postgres;Password=stock1234' \
  dotnet test StockSync.Inventory.sln
```

Run the same commands from `OrdersService` with `StockSync.Orders.sln` for the orders service. Without that variable, PostgreSQL integration checks are skipped. Each integration check creates and removes its own database rather than changing application data. The gateway has no test directory; use `npm run typecheck` and `npm run build` to check it.
