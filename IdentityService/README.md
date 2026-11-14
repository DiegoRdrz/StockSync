# StockSync Identity Service

Multi-tenant authentication and user management service for the **StockSync** SaaS platform, built with **.NET 8** and **PostgreSQL 15**.

---

## Overview

The Identity Service acts as the authority for identity and tenant lifecycle within StockSync. It issues industry-standard JWT tokens containing the tenant context (`tenant_id`), user identifier (`sub`), and user role (`Owner` or `Employee`), which are consumed by the API Gateway (YARP) and forwarded downstream to services such as `InventoryService`.

### Key Features
- **Clean Architecture**: Domain → Application → Infrastructure → Web API.
- **Strictly in English**: All domain models, tables, endpoints, DTOs, and error messages are in English.
- **Multi-Tenant Authority**: Tenants are first-class entities; users belong to a tenant.
- **Role-Based Access Control**: `Owner` (company administrator) and `Employee` (staff).
- **Secure Password Hashing**: Cryptographic hashing via BCrypt (work factor 11).
- **JWT Token Issuance**: Standard HS256 tokens with configurable expiration and claims.
- **RFC 7807 ProblemDetails**: Centralized exception handling and automatic model validation.
- **Automatic Migrations & Demo Seeder**: Configurable runtime migrations and idempotent demo data seeding (`--seed` / `--migrate`).

---

## Running with Docker Compose

From the repository root (`StockSync/`):

```bash
docker compose up -d --build identity-api
```

Swagger UI will be accessible at:
- **<http://localhost:5002/swagger>**

### Viewing Logs and Managing Containers
```bash
docker compose logs -f identity-api   # Follow API logs
docker compose stop identity-api     # Stop service
```

---

## Running Locally with the .NET SDK

### Prerequisites
- .NET 8 SDK
- PostgreSQL running locally on port `5433` (or via `docker compose up -d identity-db`)

### Commands
From the repository root:
```bash
# 1. Start the Identity PostgreSQL database
docker compose up -d identity-db

# 2. Navigate to IdentityService
cd IdentityService

# 3. Apply pending migrations
dotnet ef database update \
  --project StockSync.Identity.Infrastructure \
  --startup-project StockSync.Identity.WebApi

# 4. Seed demo data and run
dotnet run --project StockSync.Identity.WebApi --launch-profile http -- --seed
dotnet run --project StockSync.Identity.WebApi --launch-profile http
```

Swagger UI when running locally:
- **<http://localhost:5042/swagger>**

---

## Demo Credentials (Seeded by Default)

When running in `Development` with demo seeding enabled, the following accounts and tenant are seeded:

| Entity | Identifier / Email | Password | Role | Description |
|---|---|---|---|---|
| **Default Tenant** | `00000000-0000-0000-0000-000000000001` | — | — | Matches InventoryService default tenant |
| **Demo Owner** | `owner@stocksync.demo` | `Password123!` | `Owner` | Full management access |
| **Demo Employee** | `employee@stocksync.demo` | `Password123!` | `Employee` | Standard staff access |

---

## API Endpoints Summary

### Authentication (`/api/auth`)
- `POST /api/auth/register-tenant`: Registers a new tenant organization and its initial `Owner` user.
- `POST /api/auth/login`: Authenticates email and password, returning an access token and user/tenant profile.
- `GET /api/auth/me` *(Bearer)*: Returns the currently authenticated user and tenant details.

### Users (`/api/users`)
- `GET /api/users` *(Bearer)*: Lists all users in the caller's tenant with pagination and optional search/role filters.
- `GET /api/users/{id}` *(Bearer)*: Retrieves user profile details.
- `POST /api/users` *(Bearer: Owner)*: Creates a new user/employee in the caller's tenant.
- `PUT /api/users/{id}` *(Bearer: Owner)*: Updates user profile or role.
- `PUT /api/users/{id}/password` *(Bearer)*: Changes a user's password.
- `DELETE /api/users/{id}` *(Bearer: Owner)*: Deactivates an employee account.

### Tenants (`/api/tenants`)
- `GET /api/tenants/current` *(Bearer)*: Retrieves current tenant information.
- `PUT /api/tenants/current` *(Bearer: Owner)*: Updates company name and tax identification.

---

## Running Automated Tests

From the `IdentityService` folder:

```bash
# Run unit tests
dotnet test StockSync.Identity.UnitTests

# Run all tests (including integration tests if PostgreSQL connection string is set)
dotnet test StockSync.Identity.sln
```

