# StockSync API Gateway

TypeScript + NestJS gateway using Fastify. It publishes the inventory service through one HTTP entry point without duplicating inventory rules or accessing PostgreSQL.

```text
Client / Swagger → gateway :9090 → InventoryService → PostgreSQL
```

## Start with Docker

From the StockSync repository root:

```sh
docker compose up -d --build
```

Open http://localhost:9090/ to reach Swagger. The internal backend is `http://inventory-api:8080`; its direct development endpoint is http://localhost:5001. The gateway can start before inventory initialization finishes. `/health` checks the gateway process, not the backend or database.

## Run locally

Requires Node.js 24 or later. Start InventoryService first, then:

```sh
cd ApiGateway
nvm install && nvm use  # Optional, when using nvm.
cp -n .env.example .env
npm ci
npm run dev
```

Use `INVENTORY_API_URL=http://localhost:5001` for inventory in Docker or `http://localhost:5041` for local .NET. Restart after changing `.env`. Use `npm run build` followed by `npm start` for compiled execution. Avoid running the local and Docker gateways on the same port; `docker compose stop api-gateway` stops the container.

## Routes and behavior

| Route | Purpose |
| --- | --- |
| `/` | Redirect to `/swagger` |
| `/health` | Gateway process status |
| `/api/inventory/products` and subpaths | Product operations |
| `/api/inventory/categories` and subpaths | Category operations |
| `/api/inventory/stock` and subpaths | Assignments, receipts, issues, adjustments and history |
| `/api/inventory/movements` and subpaths | General movement listing |
| `/api/orders` and subpaths | Sales orders on OrdersService (same path upstream) |
| `/api/purchase-orders` and subpaths | Purchase orders on OrdersService (same path upstream) |
| `/swagger` and subpaths | Swagger UI and OpenAPI JSON |

Orders routes keep their path. The gateway rewrites `/api/inventory/...` to `/api/...`. For example, `/api/inventory/products` reaches `/api/products` on InventoryService. It preserves methods, request bodies, queries and backend status codes. The backend decides which operations it supports. Legacy Spanish routes are not exposed.

```sh
curl http://localhost:9090/api/inventory/products
curl http://localhost:9090/api/inventory/categories
curl http://localhost:9090/api/inventory/movements
curl http://localhost:9090/api/inventory/stock/branch/11111111-1111-1111-1111-111111111111
```

## Swagger

Only the OpenAPI JSON document is transformed. Paths use `/api/inventory/...` and root `servers` becomes `[{"url":"/"}]`. Internal route/operation servers and the gateway-managed tenant header parameter are removed. Swagger's Try it out therefore targets the gateway origin.

Other documentation is preserved. Documents are limited to 2 MiB; invalid documents return 502. Inventory responses are streamed without transformation. This same-origin Swagger flow does not require permissive CORS.

## Configuration

| Variable | Default |
| --- | --- |
| `HOST` | `127.0.0.1` |
| `PORT` | `9090` |
| `INVENTORY_API_URL` | `http://localhost:5001` |
| `ORDERS_API_URL` | `http://localhost:5003` |
| `UPSTREAM_TIMEOUT_MS` | `10000` |
| `TENANT_ID` | `00000000-0000-0000-0000-000000000001` |
| `INVENTORY_CA_FILE` | Optional PEM file for an HTTPS backend |

Docker uses `GATEWAY_PORT` and `GATEWAY_TENANT_ID` from the root `.env`. Recreate containers after configuration changes with `docker compose up -d`.

The gateway uses a fixed development tenant and overwrites `X-Tenant-Id`. It does not authenticate users or resolve tenants per user. For production multitenancy, add authentication and authorized tenant resolution and restrict direct backend access.

### Optional HTTPS

Docker and the inventory HTTP profile do not need certificates. For `https://localhost:7079`, Node validates TLS. Export the development certificate and configure `INVENTORY_CA_FILE` if needed:

```sh
mkdir -p certs
dotnet dev-certs https --export-path certs/inventory.pem --format PEM
```

`certs/` and `.env` are excluded from Git and Docker context. TLS verification remains enabled; Node.js 24 supports the `--use-system-ca` startup option.

## Structure

```text
src/
  main.ts                       Process entry point
  composition/                  Nest/Fastify startup and dependency registration
  application/
    routing/                    Public routes and upstream mapping
    documentation/              OpenAPI publication policy
    health/                     Process status
  infrastructure/
    configuration/              Environment, certificates and injected options
    proxy/                      Streaming, headers, timeouts and upstream errors
  presentation/http/
    controllers/                Root and health endpoints
    filters/                    Nest exceptions as Problem Details
```

`presentation` and `infrastructure` depend on `application`. `composition` wires concrete implementations. The application layer does not import NestJS, Fastify or environment variables. Inventory entities, repositories and business rules belong to InventoryService, so the gateway has no empty domain layer.

Proxy routes use Fastify plugin hooks; Nest controller guards and filters do not automatically apply to them.

```sh
npm run typecheck  # Check server types without generating output.
npm run build     # Compile src/ into dist/.
npm start         # Run dist/main.js.
```

Docker includes compiled server code and production dependencies. There are no test or auxiliary script folders.

## Errors and request handling

Connection failures return 502. Timeouts before headers return 504; timeouts after the response starts interrupt the connection. The timeout limits connection establishment, headers and body inactivity rather than total continuous-stream duration. Requests are not automatically retried.

The gateway generates `X-Request-Id`, replaces client forwarding headers, and preserves backend errors such as 400, 404 and 409. Logs exclude bodies and authorization headers.

If PostgreSQL reports `28P01: password authentication failed`, set the root `POSTGRES_PASSWORD` to the existing volume's password and recreate inventory with `docker compose up -d --no-build inventory-api`. Changing an environment variable does not change an already-initialized database password.

References: [NestJS providers](https://docs.nestjs.com/providers) and [Fastify HTTP proxy](https://github.com/fastify/fastify-http-proxy).
