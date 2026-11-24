# StockSync Web

React frontend for every StockSync microservice, served through the API Gateway.

| | |
|---|---|
| Stack | React 19, TypeScript, Vite 8 |
| UI | Mantine 9 (core, form, dates, charts, modals, notifications, spotlight), Tabler Icons |
| Data | TanStack Query 5, React Router 7 |
| Locale | Spanish (Colombia), COP currency |

## Run

With Docker (whole stack, including this app):

```sh
docker compose up -d --build
# http://localhost:8080
```

`nginx` serves the build and forwards `/api` to `api-gateway:9090`. Set `WEB_PORT` in the root `.env` to change the port.

Local development against a running gateway:

```sh
cd frontend
npm install
npm run dev        # http://localhost:5173, proxies /api to http://localhost:9090
```

Set `VITE_GATEWAY_URL` in `frontend/.env.local` to point the proxy at another gateway. The gateway has no CORS configuration, so the app always calls `/api` on its own origin.

Demo accounts (seeded by IdentityService): `owner@stocksync.demo` and `employee@stocksync.demo`, both with `Password123!`. The login screen has buttons that fill them in.

## Screens

| Screen | Route | Service | Owner only |
|---|---|---|---|
| Login / register company | `/login`, `/register` | Identity | |
| Summary | `/` | Reporting, Inventory | Sales KPIs |
| Stock per branch | `/stock` | Inventory | Receipts, issues, adjustments, assignments |
| Products, categories | `/products`, `/categories` | Inventory | Create, edit, deactivate |
| Movements | `/movements` | Inventory | |
| Sales orders | `/orders`, `/orders/:id` | Orders | Confirm, cancel |
| Purchase orders | `/purchase-orders`, `/purchase-orders/:id` | Orders | Whole section |
| Reports | `/reports` | Reporting | Sales summary and detail, purchases |
| Notifications, recipients | `/notifications`, `/notifications/recipients` | Notification | Recipient changes |
| Users | `/users` | Identity | Create, edit, deactivate |
| Settings | `/settings` | Identity | Company data |

Employees see Owner-only actions disabled with an explanation instead of hitting a 403.

## Branches

There is no branch service yet. The app keeps a local catalogue (browser storage) that gives names to branch IDs, preloaded with the two demo branches. Manage it in **Settings → Branches**. The branch picker in the header sets the active branch for stock, new orders and the summary.

## Scripts

```sh
npm run typecheck
npm run build
```

See [CONVENTIONS.md](CONVENTIONS.md) for structure and UI rules.
