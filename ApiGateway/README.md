# StockSync API Gateway

TypeScript and NestJS gateway using Fastify. It serves as the HTTP entry point for Identity, Inventory, Orders and Reporting.

## Start with Docker

Provision `JWT_SECRET_KEY` using the [root startup instructions](../README.md#run-with-docker), then run from the repository root:

```sh
docker compose up -d --build
```

Open http://localhost:9090/ for Inventory Swagger. Reporting is available through `/api/reports`. The gateway can start while backends initialize. `/health` checks the gateway process.

## Run locally

Requires Node.js 24 or later. Start the backends for the routes you need, then:

```sh
cd ApiGateway
nvm install && nvm use  # Optional, when using nvm.
cp -n .env.example .env
npm ci
npm run dev
```

The example environment points to the local .NET HTTP profiles. Set `JWT_SECRET_KEY` and Identity's `Jwt__SecretKey` to the same private value. Supply matching issuer and audience values when overriding the defaults. Restart after configuration changes. For compiled execution use `npm run build` and `npm start`.

## Routes and behavior

| Route | Purpose |
| --- | --- |
| `/` | Redirect to `/swagger` |
| `/health` | Gateway process status |
| `/api/auth` and subpaths | Identity authentication (`login`, `register-tenant`, `me`) |
| `/api/users` and subpaths | User and employee management |
| `/api/tenants` and subpaths | Tenant company metadata |
| `/api/inventory/products` and subpaths | Product operations |
| `/api/inventory/categories` and subpaths | Category operations |
| `/api/inventory/stock` and subpaths | Assignments, receipts, issues, adjustments and history |
| `/api/inventory/movements` and subpaths | General movement listing |
| `/api/orders` and subpaths | Sales orders on OrdersService (same path upstream) |
| `/api/purchase-orders` and subpaths | Purchase orders on OrdersService (same path upstream) |
| `/api/reports` and subpaths | Sales, purchases and inventory reports on ReportingService (same path upstream) |
| `/swagger` and subpaths | Swagger UI and OpenAPI JSON |

The Inventory prefix `/api/inventory` is rewritten to `/api`. Identity aliases under `/api/identity` map to `/api`. Orders and Reporting paths are preserved.

The gateway verifies the HS256 access token and replaces any caller-supplied `X-Tenant-Id` with the verified `tenant_id`. Missing or invalid credentials return 401. Orders also requires this tenant to match `TENANT_ID` and returns 403 for a different tenant. Inventory and Reporting use the verified tenant for each request.

```sh
curl -H "Authorization: Bearer ${ACCESS_TOKEN}" http://localhost:9090/api/inventory/products
```

Public routes are GET /, GET /health, development Swagger reads, and POST login and register-tenant under both `/api/auth` and `/api/identity/auth`. All other proxied API routes require a bearer access token. Browser preflight terminates at the gateway. Swagger uses the same origin.

Before forwarding, the proxy checks the HS256 signature, issuer, audience, expiration and any not-before time. It requires non-empty GUIDs in `sub` and `tenant_id`. Identity issues access tokens with an eight-hour default lifetime and currently provides no refresh flow. The gateway discards caller tenant headers on public requests. Only protected Identity requests receive the original bearer credential. Owner-only Identity writes and password changes by the owner or the user are checked at the gateway and again by Identity.

## Swagger

Only the OpenAPI JSON document is transformed. Paths use `/api/inventory/...` and root `servers` becomes `[{"url":"/"}]`. Internal route/operation servers and the gateway-managed tenant header parameter are removed. Swagger's Try it out therefore targets the gateway origin.

Other documentation is preserved. Documents are limited to 2 MiB; invalid documents return 502. Inventory responses are streamed without transformation. This same-origin Swagger flow does not require permissive CORS.

## Configuration

| Variable | Default |
| --- | --- |
| `JWT_SECRET_KEY` | Required private signing key of at least 32 UTF-8 bytes |
| `JWT_ISSUER` | `StockSync.Identity` |
| `JWT_AUDIENCE` | `StockSync.Ecosystem` |
| `HOST` | `127.0.0.1` |
| `PORT` | `9090` |
| `INVENTORY_API_URL` | `http://localhost:5001` |
| `IDENTITY_API_URL` | `http://localhost:5002` |
| `ORDERS_API_URL` | `http://localhost:5003` |
| `REPORTING_API_URL` | `http://localhost:5004` |
| `UPSTREAM_TIMEOUT_MS` | `10000` |
| `TENANT_ID` | `00000000-0000-0000-0000-000000000001` |
| `INVENTORY_CA_FILE` | Optional PEM file for an HTTPS backend |

Docker uses `GATEWAY_PORT` and `GATEWAY_TENANT_ID` from the root `.env`. Recreate containers after configuration changes with `docker compose up -d`.

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

Run `npm test` after building. The tests use signed credentials and recording upstreams. For the live workflow test set `STOCKSYNC_TEST_GATEWAY` to an isolated loopback gateway URL and provide `STOCKSYNC_TEST_OWNER_EMAIL` and `STOCKSYNC_TEST_OWNER_PASSWORD` for its configured Orders tenant. The test registers another tenant, creates business records and waits for their projections.

The default Compose deployment trusts the Docker host and services on each private network. The business APIs trust the forwarded tenant header. Identity internally gives that header precedence over its claim. Keep those listeners out of untrusted networks. Local .NET HTTP profiles are intended for trusted development machines. Possession of the HS256 verification key also permits signing. Previously issued tokens may retain their privileges until expiration.

## Errors and request handling

Connection failures return 502. Timeouts before headers return 504; timeouts after the response starts interrupt the connection. The timeout limits connection establishment, headers and body inactivity rather than total continuous-stream duration. Requests are not automatically retried.

The gateway generates `X-Request-Id`, replaces client forwarding headers, and preserves backend errors such as 400, 404 and 409. Logs exclude bodies and authorization headers.

If PostgreSQL reports `28P01: password authentication failed`, set the root `POSTGRES_PASSWORD` to the existing volume's password and recreate inventory with `docker compose up -d --no-build inventory-api`. Changing an environment variable does not change an already-initialized database password.

References: [NestJS providers](https://docs.nestjs.com/providers) and [Fastify HTTP proxy](https://github.com/fastify/fastify-http-proxy).
