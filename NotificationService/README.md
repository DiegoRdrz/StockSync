# NotificationService

NotificationService reacts to integration events from `OrdersService` and `InventoryService` on the `stocksync.events` RabbitMQ topic exchange, dispatching Email and WhatsApp alerts and recording tenant-scoped delivery history in `stocksync_notification`.

## Configuration and startup

| Setting | Development value or behavior |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string for `stocksync_notification`. Local development uses port `5436`. |
| `RabbitMq__ConnectionString` | AMQP URI. The local .NET profile connects to the Compose broker at `amqp://stocksync:stocksync@localhost:5672` by default. If unset, the API serves existing notification records and consumes no events. |
| `Initialization__ApplyMigrations` | Compose sets it to `true` so the service migrates before accepting traffic or consuming events. |
| `Initialization__LoadDemoData` | Seeds default Email (`owner@stocksync.demo`) and WhatsApp (`+15550100100`) recipients for the default tenant in Development. |
| `Smtp__Host`, `Smtp__Port`, `Smtp__FromAddress` | Optional SMTP relay configuration. When `Smtp__Host` is omitted, emails are dispatched in simulated mode and logged. |
| `WhatsApp__WebhookUrl` | Optional HTTP webhook for WhatsApp messages. When omitted, WhatsApp alerts are dispatched in simulated mode and logged. |

From the repository root, start Notification and its dependencies with Docker:

```sh
docker compose up -d --build notification-api
```

Use port `5005` for the direct API and development Swagger (`http://localhost:5005/swagger`). The gateway also exposes `/api/notifications` on port `9090`.

For a local .NET run, start `notification-db` and `rabbitmq` with `docker compose up -d notification-db rabbitmq`. From `NotificationService`, apply migrations, seed demo recipients, and start the HTTP profile:

```sh
dotnet run --project StockSync.Notification.WebApi --launch-profile http -- --migrate
dotnet run --project StockSync.Notification.WebApi --launch-profile http -- --seed
dotnet run --project StockSync.Notification.WebApi --launch-profile http
```

The local HTTP profile uses port `5047` (`http://localhost:5047/swagger`).

## Event consumption and dead-lettering

NotificationService binds durable queues to `stocksync.events` and dead-letters failed or malformed messages to the durable direct exchange `notification.dlx`.

| Queue | Routing key | Error queue on `notification.dlx` | Trigger condition |
| --- | --- | --- | --- |
| `notification.orders` | `orders.order-created.v1` | `notification.orders.error` | Always sends Email and WhatsApp order-created alerts |
| `notification.stock-movements` | `inventory.stock-movement-recorded.v1` | `notification.stock-movements.error` | Sends Email and WhatsApp low-stock alerts when `minimumStock > 0` and `newQuantity < minimumStock` after crossing or decreasing below the threshold |

Each delivery validates the routing key, AMQP `type`, `message_id` and `tenant-id` header against the JSON payload. Deduplication uses the `"ProcessedEvents"` primary key `(ConsumerName, EventId)` in the same database transaction as the notification records before acknowledging the message.

## HTTP endpoints

| Route | Purpose |
| --- | --- |
| `GET /api/notifications` | Paginated notification history (`channel`, `eventType`, `status`, `branchId`, `from`, `to`, `page`, `pageSize`) |
| `GET /api/notifications/{id}` | Get one notification by ID |
| `GET /api/notifications/recipients` | Paginated list of active notification recipients (`channel`, `page`, `pageSize`) |
| `GET /api/notifications/recipients/{id}` | Get one active recipient by ID |
| `POST /api/notifications/recipients` | Register an Email or WhatsApp recipient (`name`, `channel`, `destination`) |
| `PUT /api/notifications/recipients/{id}` | Update an active recipient |
| `DELETE /api/notifications/recipients/{id}` | Soft-delete (`Deactivate`) a recipient |

## Verification

From `NotificationService`:

```sh
dotnet build StockSync.Notification.sln
dotnet test StockSync.Notification.sln
```

Set `STOCKSYNC_TEST_POSTGRES` to run isolated PostgreSQL integration tests and `STOCKSYNC_TEST_RABBITMQ` to run live RabbitMQ consumer tests.
