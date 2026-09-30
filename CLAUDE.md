# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**OrderPoint** — a multi-tenant **SaaS backend** built as a .NET 10 Modular Monolith: Clean
Architecture + Vertical Slice Architecture (VSA) + CQRS (MediatR) +
FluentValidation, targeting PostgreSQL, with Minimal APIs. Six business
modules are implemented: **Identity** (tenants, users, invitations, plans,
token issuance), **Catalog**, **Inventory**, **Sales**, **Notifications**,
**Reporting**. They collaborate through a choreographed order saga over the
transactional outbox.

**Read [`DESIGN.md`](DESIGN.md) first.** It is the authoritative explanation of
every architectural decision here (DbContext-per-schema strategy, migrations
strategy, cross-module transactions/outbox/saga, sync vs. async inter-module
communication, authz model, tenancy model, package choices) with file
references into this repo. This CLAUDE.md is a navigation and command aid, not
a duplicate of it — don't re-derive architectural rationale here, go read DESIGN.md.

## Commands

```bash
# Build the whole solution
dotnet build

# Fast unit + architecture tests (no external dependencies)
dotnet test tests/Sales.Tests
dotnet test tests/Architecture.Tests      # module-boundary rules on compiled assemblies

# Run a single test
dotnet test tests/Sales.Tests --filter "FullyQualifiedName~PlaceOrderHandlerTests.Handle_WhenStockAvailable_PersistsOrderAndEnqueuesIntegrationEvent"

# Integration tests (real PostgreSQL via Testcontainers — Docker must be running).
# The fixture applies scripts/migrations/*.sql, so regenerate them after a migration.
dotnet test tests/Integration.Tests

# Run everything
dotnet test

# Whole stack in Docker (Postgres + migrations + API on :8080, docs at /scalar)
docker compose up --build            # API_PORT=18080 docker compose up … if 8080 is taken

# Run the API locally against your own PostgreSQL instance
dotnet user-secrets set "Auth:SigningKey" "<a real secret, at least 32 bytes>" --project src/Host
dotnet run --project src/Host
```

### Migrations (per module, never solution-wide)

`dotnet-ef` is pinned in `.config/dotnet-tools.json` — run `dotnet tool restore` once.
Every `dotnet ef` command targets one module's `*.Infrastructure` project as
both `--project` and `--startup-project` — never `src/Host`. See DESIGN.md
section 3 for the full rationale (per-schema migration history, no shared
migrations project).

```bash
# Add a migration (Sales example — swap paths/context for any other module)
dotnet ef migrations add <Name> \
  --project src/Modules/Sales/Sales.Infrastructure \
  --startup-project src/Modules/Sales/Sales.Infrastructure \
  --context Sales.Infrastructure.Persistence.SalesDbContext \
  --output-dir Persistence/Migrations

# Check for model changes not yet captured in a migration (CI runs this for every module)
dotnet ef migrations has-pending-model-changes \
  --project src/Modules/Sales/Sales.Infrastructure \
  --startup-project src/Modules/Sales/Sales.Infrastructure \
  --context Sales.Infrastructure.Persistence.SalesDbContext

# Regenerate the idempotent SQL scripts for every module at once (used by CI/CD, docker compose and integration tests — never runtime Migrate())
./scripts/generate-migrations.ps1
```

## Architecture

### Module shape (repeats per module)

Each module is 5 projects under `src/Modules/{Name}/`:

- **`{Name}.Domain`** — entities/value objects, zero framework dependencies, zero references to other modules. May use `BuildingBlocks.Results`.
- **`{Name}.Application`** — CQRS handlers in VSA feature folders (`Features/{FeatureName}/{Command|Query}.cs`, `*Validator.cs`, `*Handler.cs`), plus event handlers for other modules' integration events. Depends on an `I{Name}DbContext` abstraction it owns — **never references EF Core, BuildingBlocks.Persistence, or Infrastructure** (see `Sales.Application/Abstractions/ISalesDbContext.cs`).
- **`{Name}.Infrastructure`** — the `{Name}DbContext : ModuleDbContext` (schema-scoped, see below), `Configurations/` (`IEntityTypeConfiguration<T>`), `Migrations/`, and implementations of the module's published sync contracts.
- **`{Name}.Contracts`** — the module's only public export: integration events it publishes, synchronous interfaces it implements for other modules to call, and its `{Name}Permissions` names. This is the *only* project another module is allowed to reference.
- **`{Name}.Endpoints`** — Minimal API endpoint mapping in matching feature folders, plus the module's `{Name}Module : IModule` (DI registration, permissions, endpoint group).

**Module isolation is enforced by project-reference discipline, the guard hook, and `tests/Architecture.Tests` — not the compiler.** Before adding any reference from one module to another, confirm it points at `*.Contracts`, not `*.Application`/`*.Domain`/`*.Infrastructure`. Verify with `dotnet list <project> reference`. The accepted asymmetry: a *consumer* may reference the *publisher's* `Contracts` project to know an integration event's shape (e.g. `Inventory.Application` → `Sales.Contracts`); two modules may reference each other's Contracts (Sales ↔ Inventory) because Contracts depend on nothing but BuildingBlocks.

### Composition root

`src/Host/Program.cs` plus `src/Host/Setup/` are the **only** places that touch
JWT validation, tenancy, rate limiting, authorization policy aggregation, and
the `OutboxProcessor` hosted service. Host references each module only through
its `*.Endpoints` project and never uses a module's `Application`/`Domain`/`Infrastructure`
namespaces — only each module's `IModule` implementation, listed in one array:

```csharp
IModule[] modules = [new IdentityModule(), new CatalogModule(), new InventoryModule(),
                     new SalesModule(), new NotificationsModule(), new ReportingModule()];
```

Adding a module means: 5 new projects in the module shape above, one
`IModule` implementation, one `Host.csproj` reference, one line in that array
(full checklist: the `add-module` skill). Integration event types are
registered by the publishing module itself
(`o.RegisterEventsFromAssembly(...)` in its `AddModule`) — Host never lists them.

### BuildingBlocks (`src/BuildingBlocks/`, `src/BuildingBlocks.Persistence/`)

Shared kernel. Deliberately free of business concepts — it holds only what
every module would otherwise reinvent identically:

- `Results/` — `Result`/`Result<T>`/`Error` with `ErrorType` (no exceptions for expected failures); `Web/ResultHttpExtensions` maps them to problem details (`result.ToHttpResult(Results.Ok)`)
- `Messaging/IIntegrationEvent` — marker (extends MediatR `INotification`) with `EventId`, `OccurredOnUtc`, `TenantId`
- `Outbox/` — `OutboxMessage`, `IOutboxWriter`, `IOutboxStore`, `OutboxProcessor` (generic `BackgroundService`; per-message scope with the event's tenant restored; retries up to `MaxAttempts`)
- `MultiTenancy/` — `ITenantContext`/`ITenantSetter`, `TenantResolutionMiddleware` (tenant from the `tenant_id` claim only)
- `Security/` — `ClaimNames`, `ICurrentUser`, `TenantRole`, `PermissionDefinition`, `IPermissionCatalog`, `JwtSettings`
- `Modules/` — `IModule`, `AddModuleApplication` (MediatR + validators + logging/validation behaviors)
- `Pagination/`, `Validation/`, `Behaviors/`
- **`BuildingBlocks.Persistence`** (EF Core; referenced only by `*.Infrastructure`/`*.Endpoints`): `ModuleDbContext` base, `IsTenantScoped()`, `ModuleOutboxStore<T>`, `AddModuleDbContext<T>()`, DB health check.

Do not add anything module-specific here. If a type needs a business concept
("Order", "Customer") it belongs in a module, not here.

### Cross-module communication (the two allowed shapes)

1. **Synchronous** — a narrow interface published in the *implementing*
   module's `Contracts` project, implemented in that module's
   `Infrastructure`, registered into DI by that module's `IModule`, and
   consumed by another module's `Application` layer via constructor
   injection. Examples: `Catalog.Contracts.IProductCatalog` (Sales prices
   orders), `Inventory.Contracts.IInventoryAvailabilityChecker` (Sales
   pre-check), `Identity.Contracts.ITenantPlanProvider` (Catalog quota).

2. **Asynchronous** — the Transactional Outbox. A module writes its business
   change and an `OutboxMessage` row in the same `SaveChangesAsync` call
   (one local transaction, no distributed transaction). `OutboxProcessor`
   polls and republishes in-process via MediatR `IPublisher.Publish`. The
   consuming module subscribes with a normal `INotificationHandler<TEvent>`,
   referencing only the publisher's `Contracts` project. **Delivery is
   at-least-once: every consumer must be idempotent** (key on OrderId/EventId
   with a unique index). The order saga: `OrderPlaced` → Inventory →
   `StockReserved`/`StockReservationFailed` → Sales → `OrderConfirmed`/`OrderRejected`
   → Notifications + Reporting; `OrderCancelled` → Inventory releases stock.

Never call another module's handler directly in-process outside these two
shapes, and never share a physical `DbTransaction`/`TransactionScope` across
two modules' `DbContext`s (see DESIGN.md section 4).

### Database & tenancy

- One physical PostgreSQL database, one connection string
  (`ConnectionStrings:Database`), shared by all modules.
- Each module's `DbContext` derives from `ModuleDbContext` and maps to exactly
  one schema (`protected override string Schema => SchemaName;`) —
  `identity`, `catalog`, `inventory`, `sales`, `notifications`, `reporting`.
  A module's `DbContext` must never map an entity into another module's schema.
- Each `DbContext` uses its own `__ef_migrations_history` table inside its own
  schema (set by `AddModuleDbContext` and the design-time factory's
  `ModulePersistenceExtensions.DesignTimeOptions`).
- **Tenant isolation is automatic:** call `builder.IsTenantScoped()` in every
  tenant-owned aggregate's configuration. That adds a `TenantId` shadow column,
  a global query filter, and stamping on insert. Never filter by tenant in
  handlers. Unique indexes lead with `ModuleDbContext.TenantIdProperty`.
  Identity's tables are the deliberate exception (sign-in precedes tenant).
- Production migrations are applied via idempotent SQL scripts
  (`scripts/generate-migrations.ps1` → `scripts/migrations/*.sql`), never via
  `Database.Migrate()` at Host startup — see DESIGN.md section 3.

### Authorization

- Token validation happens only in Host (`AddPlatformSecurity`); Identity is
  the only module that issues tokens. No module validates tokens itself.
- Each module declares `IModule.Permissions` (`PermissionDefinition(name,
  description, lowest TenantRole)`); the default `AddAuthorizationPolicies`
  turns each into a policy, and Identity grants claims per role from the
  aggregated `IPermissionCatalog`. Endpoints reference policies by name:
  `.RequireAuthorization(SalesPermissions.OrdersCreate)`.
- Permission/claim naming convention: `{module}:{resource}:{action}`,
  lowercase, colon-separated (e.g. `sales:order:create`,
  `inventory:stock:adjust`). Never reuse another module's prefix
  (Architecture.Tests enforces this).
- Fine-grained, data-dependent authorization (e.g. plan limits) belongs inside
  the Application-layer handler, returning `Error.Forbidden(...)`, not in an
  endpoint policy.

### Conventions

- Use the injected `TimeProvider`, never `DateTimeOffset.UtcNow`, in handlers.
- Expected failures return `Result.Failure(Error.X("{Area}.{Reason}", ...))`; endpoints return `result.ToHttpResult(...)`.
- JSON enums are serialized as strings (Host `ConfigureHttpJsonOptions`).

### Package versions

Central Package Management is in effect
(`Directory.Packages.props`, `ManagePackageVersionsCentrally=true`), and shared
project properties live in `Directory.Build.props`. Every `.csproj`'s
`<PackageReference>` must omit `Version` — set/bump versions only
in `Directory.Packages.props`. `Microsoft.EntityFrameworkCore*` is
deliberately pinned to `10.0.4` (not the latest `10.0.12`) to match what
`Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 depends on — bumping EF Core
without checking Npgsql's supported version reintroduces an assembly-version
conflict.

MediatR 14.x is under a commercial license above a team-size/revenue
threshold (see DESIGN.md section 7 for free alternatives if that's a
blocker).

## Claude Code tooling (`.claude/`)

- **Skills** (`.claude/skills/`): `add-feature`, `add-migration`,
  `add-integration-event`, `add-module` (user-invoked only), and
  `check-architecture`. Prefer these over ad-hoc scaffolding; they encode the
  module shape above.
- **Hooks** (`.claude/hooks/guard.cs`, a .NET 10 file-based app wired in
  `.claude/settings.json`): a PreToolUse guard that blocks edits introducing
  module-boundary violations and malformed `dotnet ef` commands, and a Stop
  hook that runs `dotnet build` when `.cs`/`.csproj` files changed. If the
  guard blocks an edit, fix the design rather than working around the hook.
  Audit the whole tree with `dotnet run .claude/hooks/guard.cs -- --scan`
  (CI runs this too).
