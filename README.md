# OrderPoint — multi-tenant commerce operations SaaS on .NET 10

[![CI](https://github.com/siqbalk/OrderPoint/actions/workflows/ci.yml/badge.svg)](https://github.com/OWNER/REPO/actions/workflows/ci.yml)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-336791)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**OrderPoint** is a production-shaped **multi-tenant SaaS** for commerce
operations, built as a **modular monolith**. Organisations sign up, invite their
team, manage a catalog and stock, and take orders. Six independent modules
cooperate through a transactional outbox, and every module can be extracted
into its own service later without a rewrite.

**Clean Architecture · Vertical Slice Architecture · CQRS (MediatR) · FluentValidation · EF Core + PostgreSQL · Minimal APIs · Transactional Outbox · Choreographed Saga**

---

## Highlights

- **Six modules, one deployable.** Identity, Catalog, Inventory, Sales,
  Notifications, and Reporting. Each owns its own PostgreSQL schema,
  `DbContext`, migrations, and permissions. Modules talk only through each
  other's `*.Contracts` project, and that is enforced by an architecture test
  suite and a source-level guard.
- **Real multi-tenancy.** The tenant comes from the signed JWT only. EF Core
  global query filters and automatic stamping isolate every tenant's rows,
  and queries return nothing when no tenant is set.
- **SaaS identity.** Self-service sign-up, login, email invitations with
  single-use hashed tokens, Owner/Admin/Member roles, and permissions that
  each module declares for itself.
- **Subscription plans.** Free, Pro, and Enterprise, with enforced user and
  product quotas.
- **A cross-module saga with compensation.** An order reserves stock
  all-or-nothing in another module, then is confirmed or rejected
  asynchronously. Cancelling releases the stock and reverses the revenue.
  Handlers are idempotent under at-least-once delivery.
- **Operability.** RFC 9457 problem details, per-IP and per-tenant rate
  limiting, liveness and readiness health checks, structured logging with
  tenant context, and optimistic concurrency.
- **Deploy-safe migrations.** Idempotent per-module SQL scripts are applied
  by the pipeline, never by the app at startup.
- **Tested for real.** Unit tests, architecture tests, and Testcontainers
  integration tests that drive whole journeys over HTTP against PostgreSQL,
  using the same migration scripts production runs.

## Architecture at a glance

```
                        ┌──────────────────────── Host (composition root) ────────────────────────┐
  HTTP ──▶ JWT auth ──▶ tenant resolution ──▶ rate limiting ──▶ authorization ──▶ module endpoints │
                        │                           OutboxProcessor (background)                  │
                        └──────────────────────────────────────────────────────────────────────────┘
     Identity        Catalog         Inventory          Sales          Notifications     Reporting
   (tenants,users) (products)     (stock, reserve)   (orders)          (email log)     (revenue model)
        │  sync: plan limits ▲   ▲ sync: price      ▲ sync: availability
        └────────────────────┘   └───── Sales ──────┘
   async (outbox): ProductCreated ─▶ Inventory
                   OrderPlaced ─▶ Inventory ─▶ StockReserved/Failed ─▶ Sales ─▶ OrderConfirmed/Rejected ─▶ Notifications, Reporting
                   OrderCancelled ─▶ Inventory (release), Reporting (reverse), Notifications
                   TenantRegistered, UserInvited ─▶ Notifications
   ─────────────────────────── one PostgreSQL database, one schema per module ───────────────────────────
```

Each module follows the same five-project shape:

```
src/Modules/Sales/
  Sales.Domain          entities & invariants (no framework code)
  Sales.Application     vertical slices: Features/PlaceOrder/{Command,Validator,Handler}.cs
  Sales.Infrastructure  SalesDbContext (schema "sales"), configurations, migrations
  Sales.Contracts       the module's only public surface: events, sync interfaces, permission names
  Sales.Endpoints       Minimal API endpoints + SalesModule : IModule
```

**[DESIGN.md](DESIGN.md)** explains every decision: why one DbContext per
schema, why the app never migrates itself, how the outbox and saga work, the
tenancy model, and the upgrade paths.

## Getting started

### Option A — everything in Docker (fastest)

Requires Docker.

```bash
docker compose up --build
```

This starts PostgreSQL, applies `scripts/migrations/*.sql`, and runs the API
on **http://localhost:8080**. Interactive API docs are at
**http://localhost:8080/scalar**. If port 8080 is taken, run
`API_PORT=18080 docker compose up --build`.

### Option B — run from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) and a
PostgreSQL 15+ instance on `localhost:5432` (user and password `postgres`).
You can run `docker compose up -d postgres migrate` to get one with the
schema already applied.

```bash
dotnet tool restore
dotnet user-secrets set "Auth:SigningKey" "$(openssl rand -base64 48)" --project src/Host
dotnet run --project src/Host          # http://localhost:5079/scalar
```

If you use your own database, apply the migrations first:

```bash
for f in scripts/migrations/*.sql; do psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f "$f"; done
```

### Try it

[`src/Host/Host.http`](src/Host/Host.http) walks through the whole flow. It
works with the VS Code REST Client, JetBrains HTTP Client, and Visual Studio.
With curl:

```bash
# 1. Sign up an organisation (returns an access token)
TOKEN=$(curl -s -X POST localhost:8080/api/identity/tenants/register -H 'Content-Type: application/json' \
  -d '{"tenantName":"Acme","ownerEmail":"owner@acme.test","ownerDisplayName":"Olivia","password":"correct horse battery"}' | jq -r .accessToken)
AUTH="Authorization: Bearer $TOKEN"

# 2. Add a product and receive stock
curl -s -X POST localhost:8080/api/catalog/products -H "$AUTH" -H 'Content-Type: application/json' -d '{"sku":"WIDGET-1","name":"Widget","price":12.50}'
curl -s -X POST localhost:8080/api/inventory/stock    -H "$AUTH" -H 'Content-Type: application/json' -d '{"sku":"WIDGET-1","quantity":100}'

# 3. Place an order — it is confirmed asynchronously once Inventory reserves stock
curl -s -X POST localhost:8080/api/sales/orders -H "$AUTH" -H 'Content-Type: application/json' \
  -d '{"customerName":"Ada","customerEmail":"ada@example.com","lines":[{"sku":"WIDGET-1","quantity":7}]}'

# 4. A moment later: order Confirmed, stock reserved, revenue booked, email logged
curl -s localhost:8080/api/sales/orders   -H "$AUTH"
curl -s localhost:8080/api/reporting/sales -H "$AUTH"
curl -s localhost:8080/api/notifications   -H "$AUTH"
```

Emails are written to the API log by default (`LoggingEmailSender`). Swap in a
real provider for production.

## API overview

| Module | Endpoints | Permission (lowest role) |
|---|---|---|
| Identity | `POST /api/identity/tenants/register`, `POST /auth/login`, `POST /invitations/accept` | anonymous, rate-limited |
| | `GET /api/identity/me` | any signed-in user |
| | `GET /users` · `POST /users/invitations` · `PUT /tenant/plan` | `identity:user:read` (Member) · `identity:user:manage` (Admin) · `identity:tenant:manage` (Owner) |
| Catalog | `GET /api/catalog/products[/{id}]` · `POST`, `PUT /{id}` | `catalog:product:read` (Member) · `catalog:product:manage` (Admin) |
| Inventory | `GET /api/inventory/stock[/{sku}]` · `POST` (receive), `PUT /{sku}` (stocktake) | `inventory:stock:read` (Member) · `inventory:stock:adjust` (Admin) |
| Sales | `GET /api/sales/orders[/{id}]` · `POST` · `POST /{id}/cancel` | `sales:order:read`, `sales:order:create` (Member) · `sales:order:cancel` (Admin) |
| Notifications | `GET /api/notifications` | `notifications:notification:read` (Admin) |
| Reporting | `GET /api/reporting/sales?from=&to=` | `reporting:sales:read` (Admin) |
| Platform | `GET /health/live`, `GET /health/ready`, `GET /openapi/v1.json` | anonymous |

Plans: **Free** allows 3 users and 25 products, **Pro** 25 users and 5,000
products, and **Enterprise** is unlimited.

## Testing

```bash
dotnet test                              # everything (integration tests need Docker)
dotnet test tests/Architecture.Tests     # module-boundary rules, fast
dotnet test tests/Integration.Tests      # full journeys against PostgreSQL via Testcontainers
dotnet run .claude/hooks/guard.cs -- --scan   # source-level boundary audit
```

| Project | What it covers |
|---|---|
| `Sales.Tests`, `Inventory.Tests`, `Identity.Tests`, `Catalog.Tests` | Domain rules and handlers (NSubstitute, no I/O) |
| `Architecture.Tests` | Cross-module references only via Contracts, no EF Core in Application/Domain, events live in Contracts, permission naming |
| `Integration.Tests` | Order saga end to end, tenant isolation, invitation and RBAC, plan limits, auth errors, health checks; migrations applied from the shipped SQL scripts (twice, to prove idempotency) |

CI ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)) builds the
solution, runs the guard audit, fails on migrations that are out of date,
runs every test, and builds the Docker image.

## Configuration

| Key | Purpose | Default |
|---|---|---|
| `ConnectionStrings:Database` | Shared PostgreSQL database | `localhost` / `orderpoint` |
| `Auth:SigningKey` | HMAC key for JWTs, **≥ 32 bytes; required** | — (use user-secrets or the `Auth__SigningKey` env var) |
| `Auth:Issuer`, `Auth:Audience`, `Auth:AccessTokenLifetimeMinutes` | Token settings | see `appsettings.json` |
| `Outbox:Enabled` | Run the outbox worker on this instance (exactly one per deployment) | `true` |
| `Outbox:PollingIntervalSeconds`, `BatchSize`, `MaxAttempts` | Outbox tuning | `2`, `50`, `5` |
| `RateLimiting:AuthenticationPermitsPerMinute`, `TenantPermitsPerMinute` | Abuse protection | `10`, `600` |
| `Cors:AllowedOrigins` | Browser front-ends allowed to call the API | none |
| `Notifications:AppBaseUrl` | Base URL for links in emails | `http://localhost:5079` |

## Working on the code

- **Add a feature**: follow an existing slice. `Inventory/…/Features/AddStock`
  is the reference command, and `Sales/…/Features/PlaceOrder` shows a
  cross-module call plus the outbox.
- **Change the schema**: add a migration to *that module only* (commands in
  [CLAUDE.md](CLAUDE.md)), then run `./scripts/generate-migrations.ps1` and
  commit the regenerated `.sql`.
- **Add a module**: five projects, one `IModule`, one line in `Program.cs`.
  The checklist is in `.claude/skills/add-module/SKILL.md`.
- The repo includes [Claude Code](https://claude.com/claude-code) skills and
  hooks (`.claude/`) that scaffold slices, modules, migrations, and events in
  the house style and block boundary violations as you edit.

## Roadmap / not included

These are listed so they can be tackled deliberately: refresh tokens or an
external IdP, a real email provider, billing integration behind plan changes,
multiple outbox workers (`FOR UPDATE SKIP LOCKED`) or a message broker, tenant
deletion and data export, and OpenTelemetry tracing. See
[DESIGN.md §9](DESIGN.md#9-saas-concerns).

> **MediatR licensing:** MediatR 13+ is commercially licensed above a
> team-size and revenue threshold. See DESIGN.md §7 for free alternatives.

## License

[MIT](LICENSE)
