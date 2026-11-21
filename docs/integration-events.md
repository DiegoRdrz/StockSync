# Integration events

Events are published to `stocksync.events`, a durable topic exchange with auto-delete disabled.

## Event catalog

| Routing key | Producer | Consumer | Meaning |
| --- | --- | --- | --- |
| `orders.order-created.v1` | OrdersService | NotificationService | A draft sales order was created. Triggers Email and WhatsApp order alerts. |
| `orders.sale-confirmed.v1` | OrdersService | ReportingService | Informational sale record. Inventory has already issued the stock. |
| `orders.purchase-order-received.v1` | OrdersService | ReportingService | Informational purchase receipt record. Inventory has already received the stock. |
| `inventory.stock-movement-recorded.v1` | InventoryService | ReportingService, NotificationService | A committed receipt, issue, or adjustment. Triggers low-stock alerts when the new balance falls below a positive `minimumStock`. |

`SaleConfirmed` is informational. Inventory movements are the source of truth for inventory reporting. Movement events include saga compensation movements because each compensation changes stock through an actual receipt or issue. Sale and purchase events never change stock projections.

`NotificationService` consumes `orders.order-created.v1` and `inventory.stock-movement-recorded.v1` through dedicated durable queues (`notification.orders` and `notification.stock-movements`).

## Versioning and fixtures

The routing key ends in `vN`, where `N` is the contract version. Use a new versioned routing key for breaking changes, and keep the previous version available during consumer migration. The version belongs in the routing key and AMQP `type` property.

These JSON bodies are the single source of truth for producer and consumer contract tests:

- [`order-created.v1.json`](integration-events/order-created.v1.json)
- [`sale-confirmed.v1.json`](integration-events/sale-confirmed.v1.json)
- [`purchase-order-received.v1.json`](integration-events/purchase-order-received.v1.json)
- [`stock-movement-recorded.v1.json`](integration-events/stock-movement-recorded.v1.json)

## Message format

The body is UTF-8 JSON with camelCase property names. Every event includes a stable GUID `eventId`, a UTC `occurredAt`, and a GUID `tenantId`. For a stock movement, `eventId` equals `movementId` and `occurredAt` equals `movementDate`. Replay and backfill preserve both values.

| AMQP property or header | Required value |
| --- | --- |
| `message_id` | The body's `eventId` as a GUID string. |
| `type` | The versioned routing key. |
| `content_type` | `application/json`. |
| `delivery_mode` | Persistent (`2`). |
| `timestamp` | The event occurrence time in Unix seconds. |
| `tenant-id` header | The body's `tenantId` as a GUID string. |

Consumers validate that `tenant-id` matches the body's `tenantId` before processing the message.

## Outbox and delivery

The business change and its outbox row commit in one database transaction. The background publisher sends pending rows separately and marks them processed after broker confirmation. A broker outage can delay publication without preventing the business write from committing.

Delivery is at least once. Duplicates are expected after redelivery, replay, or a crash between broker confirmation and the outbox status update. Consumers use a database uniqueness constraint to deduplicate by `eventId` and commit the processed-event row and the projection in one transaction before acknowledging. For a duplicate, roll back the transaction and acknowledge the message.

Consumers must bind their durable queues before publication. The current Orders publisher uses `mandatory: false`, so RabbitMQ can confirm an event even when no queue is bound to receive it.

Reporting is eventually consistent. A committed business change can be absent from reports until the publisher and consumer catch up, and events can arrive out of order. Update `stock_levels` only when the incoming `(occurredAt, movementId)` is greater than the stored tuple. The `movementId` tie-breaker for equal timestamps is deterministic and may differ from the actual write order.

## Manual Orders replay in development

Connect to the development Orders database (`stocksync_orders`). Replace the example event and tenant UUIDs with the intended values before running the update. The returned row identifies the event queued for replay.

```sql
UPDATE "OutboxMessages"
SET "ProcessedAt" = NULL
WHERE "Id" = 'ae093494-76d4-4509-8c26-c71774e28a24'
  AND "TenantId" = '00000000-0000-0000-0000-000000000001'
  AND "ProcessedAt" IS NOT NULL
RETURNING "Id", "TenantId", "EventType";
```

With the Orders publisher running and RabbitMQ configured, clearing `ProcessedAt` makes the selected row pending again. This republishes only rows already in `OutboxMessages`, with their original event IDs and payloads. Consumers must tolerate duplicates.
