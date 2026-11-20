# StockSync

Multi-tenant SaaS inventory, orders, identity, and reporting platform built with microservices using **.NET 8**, **TypeScript/NestJS**, **PostgreSQL 15**, and **RabbitMQ**.

## Available Services

| Service | Compose access | Local HTTP port |
| --- | --- | --- |
| API Gateway (NestJS) | localhost:9090 | 9090 |
| Inventory (.NET 8) | Internal, via `/api/inventory` | 5041 |
| Identity (.NET 8) | Internal, via `/api/auth`, `/api/users` and `/api/tenants` | 5042 |
| Orders (.NET 8) | Internal, via `/api/orders` and `/api/purchase-orders` | 5045 |
| Reporting (.NET 8) | Internal, via `/api/reports` | 5046 |

For dedicated service documentation:

- [Inventory Service Documentation](#inventory-rules)
- [Identity Service Documentation](IdentityService/README.md)
- [Orders Service Documentation](#orders-service)
- [Reporting Service Documentation](ReportingService/README.md)
- [API Gateway Documentation](ApiGateway/README.md)

## Run with Docker

Provision a random `JWT_SECRET_KEY` of at least 32 UTF-8 bytes in the environment or ignored root `.env`. Compose supplies this key to Identity and the gateway. For a new local Bash session use `export JWT_SECRET_KEY="$(openssl rand -hex 48)"`. Keep the same key across starts. Use private database and broker credentials in shared deployments.

Start Docker Desktop and run from the repository root:

```sh
cp -n .env.example .env
docker compose up -d --build
```

Open **http://localhost:9090/swagger** for Inventory Swagger. Use Authorize with an Identity access token for business operations. The default stack publishes only the gateway on loopback. For local database and broker tools add `-f docker-compose.yml -f docker-compose.dev.yml` to Compose commands. The override publishes PostgreSQL ports 5432–5435, AMQP port 5672 and management port 15672 on loopback.

Each API waits for its PostgreSQL database and applies pending migrations. Inventory and Identity load demo data when enabled. Repeated startup does not reset stock balances. Configure `POSTGRES_PASSWORD`, `LOAD_DEMO_DATA`, `GATEWAY_PORT` and `GATEWAY_TENANT_ID` in the root `.env`.

```sh
docker compose logs -f inventory-api  # Ctrl+C exits the log viewer.
docker compose stop                 # Stop services and preserve data.
docker compose up -d --build         # Rebuild after source changes.
```

`docker compose down -v` also deletes the database volume. Use it only when intentionally resetting local data.

The image enables `Initialization__ApplyMigrations=true`. With multiple API replicas, disable startup migrations and run `docker compose run --rm inventory-api --migrate` once before starting the replicas. Demo data is controlled by `Initialization__LoadDemoData` and is allowed only in Development.

![Project architecture](Assets/Diagram.png)

ReportingService is implemented. Notification remains planned.

## Run locally

Install an SDK with the .NET 8 runtime and start PostgreSQL on `localhost:5432`:

```sh
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d inventory-db
cd InventoryService
dotnet tool restore
dotnet restore
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update \
  --project StockSync.Inventory.Infrastructure \
  --startup-project StockSync.Inventory.WebApi

dotnet run --project StockSync.Inventory.WebApi --launch-profile http -- --seed
dotnet run --project StockSync.Inventory.WebApi --launch-profile http
```

Local Swagger is at http://localhost:5041/swagger. Local API listeners assume a trusted development host. Local migrations and seeding are explicit commands; ordinary `dotnet run` does not perform them automatically. Connection strings are configured through `ConnectionStrings__DefaultConnection` or development appsettings.

### Inventory movement events

Inventory saves its events in the same transaction as stock movements. Configure `RabbitMq__ConnectionString` to enable the API's background publisher. Pending events remain in the outbox when the broker is unavailable or the connection string is unset.

After applying migrations, run `dotnet run --project StockSync.Inventory.WebApi -- --backfill-events` from `InventoryService` to enqueue existing stock movements across all tenants. The command exits after enqueueing. Each event keeps the movement's original date, ID and balances, and uses current product metadata, including for inactive products. Movements whose product cannot be resolved are logged and skipped. Repeated runs preserve existing outbox records. Startup with `Initialization:LoadDemoData` enabled also backfills once after seeding.

If macOS selects a .NET installation without the .NET 8 runtime, select the installation that contains it:

```sh
export DOTNET_ROOT=/usr/local/share/dotnet
export PATH="$DOTNET_ROOT:$PATH"
dotnet --list-runtimes
```

See [the gateway README](ApiGateway/README.md) for running the gateway with Node.js 24.

## Authenticated business routes

The gateway maps `/api/inventory/...` to `/api/...` on Inventory and preserves Orders and Reporting paths.

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
| `/api/orders`, `/api/purchase-orders` | Sales and purchase order workflows |
| `/api/reports/sales` and subpaths | Sales summary, daily totals, top products and paginated sales |
| `/api/reports/purchases/summary`, `/api/reports/purchases/daily` | Received purchase totals |
| `/api/reports/inventory/movements/summary` | Movement totals by type, UTC day and product |
| `/api/reports/inventory/low-stock` | Latest known balances below minimum, paginated |

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
curl -H "Authorization: Bearer ${ACCESS_TOKEN}" http://localhost:9090/api/inventory/products
curl -H "Authorization: Bearer ${ACCESS_TOKEN}" http://localhost:9090/api/inventory/categories
curl -H "Authorization: Bearer ${ACCESS_TOKEN}" http://localhost:9090/api/inventory/stock/branch/11111111-1111-1111-1111-111111111111

# Replace with an actual stock ID.
stock_id='STOCK_ID'
curl -H "Authorization: Bearer ${ACCESS_TOKEN}" "http://localhost:9090/api/inventory/stock/$stock_id/movements"
curl -H "Authorization: Bearer ${ACCESS_TOKEN}" -i -X POST "http://localhost:9090/api/inventory/stock/$stock_id/issues" \
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

The gateway verifies the HS256 access token and replaces any caller-supplied `X-Tenant-Id` with the verified `tenant_id`. Missing or invalid credentials return 401. Orders also requires this tenant to match `TENANT_ID` and returns 403 for a different tenant. Inventory and Reporting use the verified tenant for each request.

On a fresh database, register a tenant with `POST /api/auth/register-tenant`. Login and registration return `accessToken` and the tenant ID. Set `GATEWAY_TENANT_ID` to the intended Orders tenant ID and recreate the gateway when enabling Orders for that tenant. Use the token as `Authorization: Bearer <accessToken>` for protected requests.

During upgrades, replace any previously distributed development signing key and rotate or deactivate existing demo credentials. Demo seeding is opt-in and disabling it affects future initialization. Key rotation requires users to log in again. Terminate HTTPS at a trusted edge before offering the gateway beyond localhost.

## English migration

This branch changes public routes, JSON fields, query parameters, movement values, configuration keys, source identifiers and documentation to English. Existing clients must adopt the contracts above. Rename the root environment setting to `LOAD_DEMO_DATA` when upgrading an existing `.env`.

`TranslateSchemaToEnglish` renames tables, columns, indexes and constraints in place and converts stored movement types. It preserves IDs, tenant ownership, quantities and history, with a reverse migration available. Stop older API instances before applying it because the schema and contracts change together.

Historical migrations and their old database names are intentionally retained so existing migration histories remain valid. Legacy names also appear in the upgrade/rollback migration because those are the objects it must rename. Existing user-entered product names and descriptions are data and are not automatically translated.

## Orders service

`OrdersService/` manages sales orders and supplier purchase orders with its own database (`stocksync_orders`). The gateway routes Orders requests to its internal listener. Locally Orders runs on http://localhost:5045 with `dotnet run --project StockSync.Orders.WebApi --launch-profile http` from `OrdersService` (start `orders-db`, `rabbitmq` and InventoryService first, then run `dotnet ef database update --project StockSync.Orders.Infrastructure --startup-project StockSync.Orders.WebApi` with `ASPNETCORE_ENVIRONMENT=Development`). The gateway forwards `/api/orders` and `/api/purchase-orders` unchanged. With the development override, RabbitMQ management is at http://localhost:15672 (`RABBITMQ_USER` / `RABBITMQ_PASSWORD`, default `stocksync`).

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

## Reporting service

ReportingService projects confirmed sales, received purchase orders and inventory movements into its own PostgreSQL database, `stocksync_reporting`. Orders and Inventory commit events through their transactional outboxes and publish to RabbitMQ's `stocksync.events` exchange. Reporting uses separate durable queues for sales, purchases and stock movements. Delivery is at least once and Reporting deduplicates by event ID. Report data becomes visible after the outbox publisher and consumer finish processing it.

Orders also publishes `orders.order-created.v1` for draft sales. Sales and purchases provide commercial totals. Inventory movements drive inventory reporting, including saga compensation movements. See [integration event contracts and development replay](docs/integration-events.md) and the [ReportingService runbook](ReportingService/README.md).

## Inventory service checks

From `InventoryService`:

```sh
dotnet test StockSync.Inventory.sln

# Requires a PostgreSQL role with CREATE DATABASE permission.
STOCKSYNC_TEST_POSTGRES='Host=localhost;Database=postgres;Username=postgres;Password=stock1234' \
  dotnet test StockSync.Inventory.sln
```

Run the same commands from `OrdersService` or `ReportingService` with the corresponding solution. Without that variable, PostgreSQL integration checks are skipped. Each integration check creates and removes its own database rather than changing application data. For the gateway, run `npm run typecheck`, `npm run build` and `npm test` in `ApiGateway`. Its README describes the two-tenant integration test.
