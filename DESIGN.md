# OrderPoint — Architecture & Design

.NET 10 · Clean Architecture · Vertical Slice Architecture (VSA) · CQRS (MediatR) ·
FluentValidation · PostgreSQL · Minimal APIs

This document explains the architecture implemented in this repository: a
**multi-tenant SaaS backend** built as a modular monolith. All six business
modules (**Identity**, **Catalog**, **Inventory**, **Sales**,
**Notifications**, **Reporting**) are implemented under `src/`. Together they
demonstrate every pattern described below, including a multi-step cross-module
order saga proven end to end by `tests/Integration.Tests/OrderLifecycleTests.cs`.

Each module has its own PostgreSQL schema and its own EF Core `DbContext`. The
modules share one physical database today, but the design lets any module be
extracted into its own service later without a rewrite. Every pattern below is
chosen with that extraction path in mind (see the "why" callouts). Section 9
covers the SaaS concerns layered on top: tenancy, identity, plans, and
operability.

---

## 1. Project structure

```
src/
  BuildingBlocks/                  # Thin cross-cutting kernel — see "what belongs here" below
  BuildingBlocks.Persistence/      # Shared EF Core plumbing (ModuleDbContext base, outbox store,
                                   # tenant filters). Referenced only by *.Infrastructure/*.Endpoints.
  Modules/
    Sales/
      Sales.Domain/                # Entities, value objects, invariants. No framework dependencies.
      Sales.Application/           # CQRS handlers, VSA feature folders, depends on abstractions only
        Abstractions/              # ISalesDbContext — Application never references EF Core directly
        Features/
          PlaceOrder/
            PlaceOrderCommand.cs
            PlaceOrderValidator.cs
            PlaceOrderHandler.cs
      Sales.Infrastructure/        # EF Core DbContext, migrations
        Persistence/
          SalesDbContext.cs        # : ModuleDbContext — schema, outbox, tenant isolation inherited
          Configurations/          # IEntityTypeConfiguration<T> per entity
          Migrations/              # EF Core migrations — owned exclusively by this module
      Sales.Contracts/             # Public surface other modules may reference: integration events,
                                    # permission names, sync interfaces. Sales' only allowed "export".
      Sales.Endpoints/             # Minimal API endpoint mapping + SalesModule : IModule (DI composition)
    Identity/                      # Tenants, users, invitations, plans; issues JWTs
    Catalog/                       # Products and prices; plan-limited product quota
    Inventory/                     # Stock levels and all-or-nothing reservations
    Notifications/                 # Email in reaction to other modules' events
    Reporting/                     # Revenue read model built from Sales' events
  Host/
    Program.cs                     # Composition root — see section 2
    Setup/                         # Platform wiring: auth, tenancy, rate limits, outbox worker, HTTP
    appsettings.json
tests/
  {Module}.Tests/                  # Unit tests (Sales, Inventory, Identity, Catalog), no I/O
  Architecture.Tests/              # Reflection checks of module boundaries and permission naming
  Integration.Tests/               # Testcontainers-backed, full SaaS journeys over HTTP
scripts/
  generate-migrations.ps1
  migrations/*.sql                 # Idempotent scripts — see section 3
```

| Module | Owns | Publishes | Consumes | Sync contract it offers |
|---|---|---|---|---|
| Identity | tenants, users, plans | `TenantRegistered`, `UserInvited` | — | `ITenantPlanProvider` |
| Catalog | products, prices | `ProductCreated` | — (calls `ITenantPlanProvider`) | `IProductCatalog` |
| Inventory | stock, reservations | `StockReserved`, `StockReservationFailed` | `ProductCreated`, `OrderPlaced`, `OrderCancelled` | `IInventoryAvailabilityChecker` |
| Sales | orders | `OrderPlaced`, `OrderConfirmed`, `OrderRejected`, `OrderCancelled` | `StockReserved`, `StockReservationFailed` (calls `IProductCatalog`, `IInventoryAvailabilityChecker`) | — |
| Notifications | sent-email log | — | `TenantRegistered`, `UserInvited`, `OrderConfirmed`, `OrderRejected`, `OrderCancelled` | — |
| Reporting | sales read model | — | `OrderConfirmed`, `OrderCancelled` | — |

**VSA inside each module.** A feature (e.g. `PlaceOrder`) is a self-contained
folder holding its command/query, validator, and handler together — not spread
across technical layers (`Commands/`, `Validators/`, `Handlers/` folders). The
endpoint that triggers it lives in the sibling `*.Endpoints` project, in the
same feature-folder shape, so the whole vertical slice reads top-to-bottom in
one place. Clean Architecture's layering (Domain → Application → Infrastructure)
still applies *across* projects — Application never references Infrastructure,
Domain never references anything — but *within* Application, files are grouped
by feature, not by technical role.

**What belongs in `BuildingBlocks`.** Only things every module would otherwise
reinvent identically and that have no business meaning of their own: the
`Result<T>` type, the `IIntegrationEvent`/`IOutboxWriter`/`IOutboxStore`
contracts, the generic `OutboxProcessor`, the MediatR `ValidationBehavior`, and
the `IModule` composition interface. It must never gain a business concept
("Customer", "Order") — the moment it does, it has become a seventh module in
disguise and a coupling point that defeats the "split into microservices
later" goal. Keep it small on purpose.

**How Host composes modules.** `Program.cs` never references a module's
`Application`, `Domain`, or `Infrastructure` project — only its `Endpoints`
project, and even then only through the module's `IModule` implementation
(`SalesModule`, `InventoryModule`). See [`src/Host/Program.cs`](src/Host/Program.cs)
and [`src/BuildingBlocks/Modules/IModule.cs`](src/BuildingBlocks/Modules/IModule.cs).
Adding a 7th module means adding one line to the `modules` array in
`Program.cs` — nothing else in Host changes.

---

## 2. DbContext strategy

Each module owns exactly one `DbContext`, mapped to exactly one PostgreSQL
schema, via `modelBuilder.HasDefaultSchema("sales")` in `OnModelCreating`
(see [`SalesDbContext.cs`](src/Modules/Sales/Sales.Infrastructure/Persistence/SalesDbContext.cs),
[`InventoryDbContext.cs`](src/Modules/Inventory/Inventory.Infrastructure/Persistence/InventoryDbContext.cs)).
No module's `DbContext` ever maps an entity into another module's schema —
that boundary, enforced by convention and code review (there is no compiler
check for it), is what keeps the schemas independently extractable later.

**Connection strings.** One physical database, one connection string
(`ConnectionStrings:Database` in `appsettings.json`), shared by every module's
`DbContext`. Postgres's schema-per-context isolation means six modules can
share a connection string safely — they simply never see each other's tables
because they never map them. If/when a module is extracted to its own
service, that module's DbContext swaps to its own connection string with zero
code changes beyond configuration, because the module never depended on
another module's schema existing in the same database.

**Migrations history table.** Each `DbContext` uses its own
`__ef_migrations_history` table, scoped inside its own schema:

```csharp
options.UseNpgsql(
    configuration.GetConnectionString("Database"),
    npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", SalesDbContext.Schema));
```

Without this, all six modules would fight over one `public.__EFMigrationsHistory`
table and each `dotnet ef migrations add` would see (and could clash with)
every other module's migration history.

**DI registration pattern.** Each module registers its own `DbContext` inside
its own `IModule.AddModule` — never in `Program.cs` directly — through one
shared helper, so the per-schema history table can't be forgotten:

```csharp
// src/Modules/Sales/Sales.Endpoints/SalesModule.cs
services.AddModuleDbContext<SalesDbContext>(configuration, Name, SalesDbContext.SchemaName);
services.AddScoped<ISalesDbContext>(sp => sp.GetRequiredService<SalesDbContext>());
```

[`AddModuleDbContext`](src/BuildingBlocks.Persistence/Persistence/ModulePersistenceExtensions.cs)
calls `UseNpgsql(..., npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))`,
registers the module's `IOutboxStore`, and adds a readiness health check for
the module's database. Every module DbContext derives from
[`ModuleDbContext`](src/BuildingBlocks.Persistence/Persistence/ModuleDbContext.cs).
That base class applies `HasDefaultSchema`, maps the module's own
`outbox_messages` table, implements `IOutboxWriter`, and enforces tenant
isolation (section 9).

`Sales.Application` depends on `ISalesDbContext` (an interface it owns, see
[`ISalesDbContext.cs`](src/Modules/Sales/Sales.Application/Abstractions/ISalesDbContext.cs)),
never on `SalesDbContext` or `Microsoft.EntityFrameworkCore` directly — that
keeps EF Core entirely out of the Application layer's dependency graph.

---

## 3. Migrations

**Naming.** `{Timestamp}_{Description}` is EF Core's default and is kept
as-is; the module is already disambiguated by which project/context you're
targeting, so it doesn't need to be repeated in the migration name. What must
never happen is one shared migrations project for multiple modules — each
module's migrations live only inside that module's own `*.Infrastructure`
project (`Persistence/Migrations/`).

**Creating a migration**, always scoped to one module/context:

```bash
dotnet ef migrations add InitialCreate \
  --project src/Modules/Sales/Sales.Infrastructure \
  --startup-project src/Modules/Sales/Sales.Infrastructure \
  --context Sales.Infrastructure.Persistence.SalesDbContext \
  --output-dir Persistence/Migrations
```

Subsequent migrations use the same command with a new `--output-dir`-relative
name (e.g. `AddOrderDiscountColumn`). Because `--project` and
`--startup-project` both point at the module's own Infrastructure project (not
Host), `dotnet ef` never needs to build the whole solution or resolve another
module's configuration to generate a migration — each module's design-time
factory (`SalesDbContextFactory`, `InventoryDbContextFactory`) supplies a
fixed local connection string for design-time only, independent of Host's
runtime configuration.

**Per-schema migration history.** Because each context uses its own
`__ef_migrations_history` table inside its own schema (section 2), migration
state is fully independent per module — deploying Inventory's migration `003`
has no bearing on and no dependency on Sales' migration state.

**CI/CD: idempotent SQL scripts, not runtime `Migrate()`.**

```bash
dotnet ef migrations script --idempotent \
  --project src/Modules/Sales/Sales.Infrastructure \
  --startup-project src/Modules/Sales/Sales.Infrastructure \
  --context Sales.Infrastructure.Persistence.SalesDbContext \
  --output scripts/migrations/sales.sql
```

[`scripts/generate-migrations.ps1`](scripts/generate-migrations.ps1) runs this
for every module in one pass. Each generated script:

- wraps every step in `IF NOT EXISTS (SELECT 1 FROM <schema>.__ef_migrations_history ...)`,
  so re-running it is a no-op for migrations already applied — safe to run on
  every deploy, not just the first one;
- is independent per module — applying `sales.sql` never requires
  `inventory.sql` to run first, and a deploy pipeline can apply them in
  parallel or in any order;
- is a plain, reviewable `.sql` artifact that goes through the same PR review
  and CI gate as any other change, rather than being generated and executed
  live inside the running application.

**Why not `Database.Migrate()` at startup, in production:** with more than one
replica of Host running (the normal case for anything production-grade),
every replica calling `Migrate()` on boot races to apply the same migration
concurrently — EF Core does not coordinate this across processes. It also
means a schema change ships silently as part of an app deployment, with no
separate review/approval step and no dry-run visibility into the SQL that will
execute. The idempotent-script approach applies the migration as its own
pipeline step, before the new app version starts receiving traffic, and the
generated SQL is something a reviewer actually reads in the PR diff.

`Database.Migrate()` is acceptable **only** for local development (e.g. a
`dotnet run` convenience path gated behind `if (env.IsDevelopment())`), never
wired into the production startup path in this design.

---

## 4. Cross-module transactions

**Default rule: never share a physical `DbTransaction`/`TransactionScope`
across two modules' `DbContext`s.** Doing so would require both modules'
databases to be reachable from the same process in the same transaction —
which is precisely the coupling that makes a later microservice split
impossible without a rewrite. This design has no sanctioned exception to that
rule in production code; if you find yourself reaching for
`TransactionScope` across two `DbContext`s, that's a signal the two modules
either aren't actually independent, or the operation needs to be modeled as a
saga instead (below).

**Pattern: Transactional Outbox, one per module.** A module writes its
business change and an `OutboxMessage` row in the *same* `SaveChangesAsync`
call against its *own* `DbContext` — one local ACID transaction, no
distributed transaction needed:

```csharp
// PlaceOrderHandler.cs — one SalesDbContext, one transaction
var order = Order.Place(request.CustomerName, request.Sku, request.Quantity, request.UnitPrice);
dbContext.AddOrder(order);
dbContext.Outbox.Enqueue(new OrderPlacedIntegrationEvent(...));
await dbContext.SaveChangesAsync(cancellationToken); // atomic: both rows or neither
```

A generic, module-agnostic [`OutboxProcessor`](src/BuildingBlocks/Outbox/OutboxProcessor.cs)
(`BackgroundService`) polls every module's outbox on an interval and
republishes due messages in-process via MediatR. It knows nothing about Sales
or Inventory specifically. Each module's DI registration hands it an
`EventType full name -> CLR type` map
(`o.RegisterEventsFromAssembly(typeof(SomeEvent).Assembly)` over the module's
Contracts assembly) and an `IOutboxStore`.

**Delivery semantics.** Each message is dispatched in a fresh DI scope, which
gives clean DbContexts. The event's `TenantId` is restored into that scope, so
consumers run under the same tenant isolation as the HTTP request that caused
the event. Delivery is **at-least-once**. A message whose handlers partly
succeed is retried as a whole, up to `Outbox:MaxAttempts` (default 5). After
that it is parked, with its last error kept in the row for an operator to
inspect. So every consumer is idempotent and keys on a natural id:
reservations are unique per `OrderId`, sales records use `OrderId` as their
key, and notifications are unique per `(SourceEventId, Recipient)`. Run exactly
one outbox worker per deployment: set `Outbox:Enabled=false` on HTTP-only
replicas. If you need several workers, switch the stores to
`SELECT … FOR UPDATE SKIP LOCKED` or move to a broker.

**Failure isolation matters here**: a single module's outbox being briefly
unreachable (e.g. mid-migration) must not stop every other module's outbox
from being polled, and must never bring down the whole host — a
`BackgroundService` whose `ExecuteAsync` throws stops the entire application
by default. The processor catches and logs per-module failures individually
(see the `try/catch` around `ProcessStoreAsync` in `OutboxProcessor.cs`) so
one module's transient DB issue degrades only that module's event delivery.

**Broker-free today, broker-ready later.** This design deliberately stays
broker-free: the outbox schema (`outbox_messages`: `Id`, `Type`, `Content`,
`OccurredOnUtc`, `ProcessedOnUtc`, `Attempts`, `Error`) says nothing about *how* messages
get delivered. Swapping the polling-and-publish-in-process approach for a real
broker (RabbitMQ, Azure Service Bus, Kafka) later only changes what
`OutboxProcessor` does after deserializing a message — publish to the broker
instead of calling `IPublisher.Publish` — without touching the write side
(`IOutboxWriter.Enqueue`) in any module at all.

**Multi-step processes spanning modules: sagas / process managers.** A single
integration event with one handler (e.g. Catalog → Inventory on ProductCreated) is not
a saga — it's a one-shot reaction. When a business process needs several
steps across modules *with compensation on failure* (e.g. "reserve stock, then
charge payment, then confirm shipment — release stock if payment fails"),
model it explicitly as a process manager: a stateful component (its own small
aggregate, persisted in whichever module owns the overall process, or a
dedicated `Orchestration` module) that:

1. subscribes to integration events the same way any consuming module does
   (via `Sales.Contracts`/`Inventory.Contracts`, never internal types),
2. tracks progress explicitly (a `SagaState` enum/table: `Started`,
   `StockReserved`, `PaymentCharged`, `Completed`, `Compensating`, `Failed`),
3. issues the next step as a command to the next module (again only through
   that module's public surface — a synchronous `Contracts` interface or a
   new integration event),
4. on failure, issues explicit compensating commands (`ReleaseStockCommand`)
   rather than relying on any shared transaction to roll back.

**The order flow here is a choreographed saga.** Each module reacts to the
previous step's event and publishes the next one through its own outbox. No
central orchestrator is involved, and no transaction is shared:

```
Sales:     PlaceOrder ──▶ OrderPlaced
Inventory: reserve all lines or none ──▶ StockReserved | StockReservationFailed
Sales:     Confirm ──▶ OrderConfirmed        |  Reject ──▶ OrderRejected
           (Notifications emails the customer; Reporting books revenue on Confirmed)
Sales:     CancelOrder ──▶ OrderCancelled
Inventory: release the reservation (compensation); Reporting reverses revenue
```

`Order` holds the saga state (`Placed → Confirmed | Rejected → Cancelled`), and
its transitions are idempotent, so late or duplicated replies are harmless. For
example, a reservation that lands after the customer has cancelled is ignored by
Sales and released by Inventory. The synchronous `IInventoryAvailabilityChecker`
call in `PlaceOrder` is only a fast pre-check that gives the caller immediate
feedback. The authoritative, race-free decision is the reservation. It runs
under optimistic concurrency on PostgreSQL's `xmin`, so two orders can't both
take the last unit. Once the process has enough steps that the flow gets hard
to follow (payment, shipping), switch to an explicit process manager as
described above.

---

## 5. Inter-module communication

**Synchronous: a published interface in the target module's `Contracts`
project.** Inventory owns and implements
[`IInventoryAvailabilityChecker`](src/Modules/Inventory/Inventory.Contracts/IInventoryAvailabilityChecker.cs);
Sales' `PlaceOrderHandler` takes it as a constructor dependency. Sales
references `Inventory.Contracts` — never `Inventory.Application`,
`Inventory.Domain`, or `Inventory.Infrastructure`. The concrete implementation
([`InventoryAvailabilityChecker`](src/Modules/Inventory/Inventory.Infrastructure/InternalServices/InventoryAvailabilityChecker.cs))
is registered into DI by `InventoryModule`, so Sales never even knows which
assembly implements the interface it's calling — only Host's composition root
does:

```csharp
// InventoryModule.cs — Inventory registers the implementation
services.AddScoped<IInventoryAvailabilityChecker, InventoryAvailabilityChecker>();

// PlaceOrderHandler.cs — Sales consumes only the interface
public sealed class PlaceOrderHandler(
    ISalesDbContext dbContext,
    IInventoryAvailabilityChecker availabilityChecker) : IRequestHandler<...>
```

**Asynchronous: in-process domain events vs. cross-module integration
events.** These are deliberately two different things:

- *Domain events* (plain MediatR `INotification`, not shown in this
  codebase because no module needed one yet) are for same-transaction
  side effects **within** a module — e.g. Sales reacting to its own
  `OrderPlaced` by updating a read model, still inside Sales.
- *Integration events* cross a module boundary and always go through the
  outbox (section 4) — never a direct in-process `Publish` call from one
  module's handler into another module's handler, because that would require
  a compile-time reference between them and would not survive the two
  modules being split into separate processes later.

**Worked example: Sales places an order, Inventory reserves stock, with no
project reference from Inventory to Sales' internals.**

1. Sales owns the event's shape, in its own `Contracts` project:

   ```csharp
   // src/Modules/Sales/Sales.Contracts/IntegrationEvents/OrderPlacedIntegrationEvent.cs
   public sealed record OrderPlacedIntegrationEvent(
       Guid EventId, DateTimeOffset OccurredOnUtc, Guid TenantId,
       Guid OrderId, string CustomerName, string CustomerEmail, decimal Total,
       IReadOnlyList<OrderLineItem> Lines) : IIntegrationEvent;
   ```

2. `PlaceOrderHandler` (Sales.Application) enqueues it in the same transaction
   as the `Order` row (section 4's code sample).

3. `OutboxProcessor` (BuildingBlocks, module-agnostic) picks it up, deserializes
   it using the CLR type Sales registered at startup, and calls
   `IPublisher.Publish(event)`.

4. Inventory's handler subscribes the normal MediatR way — the only thing
   Inventory depends on from Sales is the `Sales.Contracts` project, to know
   the event's shape:

   ```csharp
   // src/Modules/Inventory/Inventory.Application/Features/ReserveStockOnOrderPlaced/ReserveStockOnOrderPlacedHandler.cs
   public sealed class ReserveStockOnOrderPlacedHandler(...) : INotificationHandler<OrderPlacedIntegrationEvent>
   {
       public async Task Handle(OrderPlacedIntegrationEvent notification, CancellationToken ct)
       {
           if (await dbContext.FindReservationByOrderAsync(notification.OrderId, ct) is not null)
               return;                                             // idempotent redelivery

           var stock = await dbContext.FindBySkusAsync(skus, ct);
           var reservation = StockReservation.TryReserve(notification.OrderId, lines, stock, now);
           // success → AddReservation + Enqueue(StockReserved); failure → Enqueue(StockReservationFailed)
           await dbContext.SaveChangesAsync(ct);                  // reply + state change, one transaction
       }
   }
   ```

`dotnet list reference` confirms the boundary holds:
`Inventory.Application` references `Sales.Contracts` (to know the event
shape) and nothing else of Sales'; `Sales.Infrastructure` has **no**
reference to anything Inventory at all. This asymmetric dependency — a
consumer depending on the publisher's contract project — is the accepted
shape for event-carried data; a *publisher* depending on a *consumer's*
project would be the actual violation, and does not happen anywhere in this
codebase.

`tests/Integration.Tests/OrderLifecycleTests.cs` proves the whole saga end to
end against a real (Testcontainers) PostgreSQL instance, over HTTP:
product → stock record → order → confirmation → revenue → emails →
cancellation → stock released → revenue reversed.
`tests/Architecture.Tests` checks the reference graph of the compiled
assemblies on every CI run.

---

## 6. AuthN / AuthZ

**Token validation lives in Host only; issuance lives in Identity.** Host's
`AddPlatformSecurity` (`src/Host/Setup/`) is the only place
`AddAuthentication().AddJwtBearer(...)` is called; no module validates a token
itself. The Identity module is the only one that *issues* tokens
(`JwtTokenIssuer`, HMAC-SHA256). Both sides bind the same validated `Auth`
settings (`JwtSettings`), and a missing or short signing key fails at startup.
Swapping in an external IdP (Entra ID, Auth0, Keycloak) replaces the issuer
and Host's validation parameters. Other modules are unaffected, because they
only ever see the `tenant_id` and `permission` claims. This keeps every module's endpoints trusting the same identity
established once, at the composition root, and keeps token/JWKS/issuer
configuration in exactly one place — important because if a module is later
extracted to its own service, *that* service becomes responsible for its own
token validation, and having validation logic scattered across modules today
would mean untangling it module-by-module later instead of just standing up
the same Host-level configuration in the new service.

**Coarse-grained, policy-based authorization — centrally enforced, but
module-owned.** Each module declares the permissions it owns, and the lowest
built-in role that receives each one, via its `IModule` implementation:

```csharp
// SalesModule.cs
public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
[
    new(SalesPermissions.OrdersRead,   "View orders",   TenantRole.Member),
    new(SalesPermissions.OrdersCreate, "Place orders",  TenantRole.Member),
    new(SalesPermissions.OrdersCancel, "Cancel orders", TenantRole.Admin),
];
```

That single list drives both sides of authorization:

- **Enforcement.** `IModule.AddAuthorizationPolicies` (default interface
  implementation) turns each permission into a policy of the same name that
  requires the matching `permission` claim. Host aggregates every module's
  policies at startup, and endpoints reference them by name:
  `.RequireAuthorization(SalesPermissions.OrdersCreate)`.
- **Granting.** Host builds an `IPermissionCatalog` from every module's list.
  Identity uses it to put the right `permission` claims in a user's token for
  their role (Owner ⊇ Admin ⊇ Member), so Identity never hard-codes another
  module's permissions.

This keeps *ownership* of "what permissions exist for this module" inside the
module (so adding a new Sales permission doesn't require editing Host), while
*enforcement* stays centralized and consistent — every endpoint in every
module goes through the same ASP.NET Core authorization middleware, not a
module-specific reimplementation.

**Fine-grained, business-rule authorization lives in the Application layer,**
not in endpoint attributes or policies — because "can this specific user
discount this specific order by more than 10%" depends on data the
authorization middleware doesn't have (the order, the user's role *relative
to that order*, business thresholds that may themselves be data-driven). That
belongs inside the command handler (or a MediatR pipeline behavior scoped to
that module), where the full request and domain state are available, e.g.:

```csharp
public async Task<Result<...>> Handle(ApplyDiscountCommand request, ...)
{
    if (request.DiscountPercent > 10 && !await _permissions.HasAsync(request.UserId, "sales:order:discount:large"))
        return Result.Failure<...>(Error.Validation("Orders.DiscountNotAllowed", "..."));
    ...
}
```

**Claims/permission naming convention:** `{module}:{resource}:{action}`,
lowercase, colon-separated — e.g. `sales:order:create`,
`inventory:stock:adjust`, `inventory:stock:read`. This namespaces every
permission by the module that owns it, so two modules can never accidentally
collide on a permission name, and it reads directly as "which module do I
ask if this claim looks wrong". Each module's `Permissions` list is where its
names are defined, and `tests/Architecture.Tests` fails the build if a name
breaks the convention, uses another module's prefix, or is declared twice.
Plan limits (users, products) are a working example of data-dependent
authorization in a handler: `InviteUserHandler` and `CreateProductHandler`
return `Error.Forbidden("…PlanLimitReached")`.

---

## 7. Recommended NuGet packages

| Concern | Package(s) | Why this over alternatives |
|---|---|---|
| Mediator / CQRS | **MediatR** 14.x | Most mature, most examples, minimal ceremony for the command/query/notification shapes this design needs. **Note:** v13+ is under a commercial license — free for small teams/individuals, paid above a revenue/team-size threshold (check current terms before committing). If that's a blocker, **Wolverine** is the free/OSS alternative with built-in outbox/saga support, at the cost of a steeper learning curve and a different (source-generator-based) handler model; a hand-rolled `IRequestHandler<T>` + reflection-based dispatcher is also viable given how small the surface area is (a handful of interfaces), if avoiding both licensing and a third-party mediator entirely is a hard requirement. |
| Validation | **FluentValidation** + **FluentValidation.DependencyInjectionExtensions** | De facto standard; composes cleanly with a MediatR pipeline behavior (`ValidationBehavior<T,R>`, in `BuildingBlocks`); far more testable than data-annotation attributes for anything beyond trivial rules. |
| ORM / DB provider | **Microsoft.EntityFrameworkCore** + **Npgsql.EntityFrameworkCore.PostgreSQL** | EF Core is the standard .NET ORM with first-class per-schema, per-context migration support this design depends on; Npgsql is the only production-grade PostgreSQL provider. **Pin versions via `Directory.Packages.props`** — Npgsql's provider trails `Microsoft.EntityFrameworkCore`'s own release cadence, and taking the "latest" of each independently (as `dotnet add package` does by default) can pull mismatched `Microsoft.EntityFrameworkCore.Relational` versions into the same dependency graph, as happened while building this skeleton. |
| Outbox / messaging | Hand-rolled (`BuildingBlocks.Outbox` — `OutboxMessage`, `IOutboxWriter`, `IOutboxStore`, `OutboxProcessor : BackgroundService`) | Given the broker-free decision, a full framework (MassTransit, Wolverine's transactional outbox, NServiceBus) is more machinery than the requirement calls for — those all assume you're publishing to a broker. The hand-rolled version here is ~150 lines total and keeps the outbox schema and dispatch logic broker-agnostic by construction. **Graduate to MassTransit (with its outbox + a real transport) once you actually stand up RabbitMQ/Azure Service Bus** — at that point the abstraction (`IOutboxWriter.Enqueue`) doesn't change, only what's behind `OutboxProcessor`. |
| Result pattern | Hand-rolled (`BuildingBlocks.Results` — `Result`, `Result<T>`, `Error`) | The surface area needed (success/failure + an error code/message) is ~40 lines; a dependency like `FluentResults` or `ErrorOr` adds little beyond what's here, and a first-party type keeps `Result<T>`'s shape exactly matched to this codebase's error-handling conventions (e.g. `Error.Validation/NotFound/Conflict` categories used directly in endpoint mapping). |
| Testing | **xUnit**, **Testcontainers.PostgreSql**, **Microsoft.AspNetCore.Mvc.Testing** (`WebApplicationFactory`), **NSubstitute** | xUnit is the .NET default; Testcontainers gives integration tests a real PostgreSQL instance (catching schema/SQL issues mocks can't) without a shared dev database; `WebApplicationFactory<Program>` hosts the real `Host` composition in-process for true end-to-end HTTP tests (see `OrderLifecycleTests`); NSubstitute over Moq purely for its lower-ceremony syntax — either is fine. |
| Logging | **Serilog** (`Serilog.AspNetCore`, `Serilog.Sinks.Console`) | Structured logging is close to mandatory for a multi-module system — you need to filter "show me only Inventory's logs" or "show me this correlation ID across both modules' outbox processing," which plain `ILogger` text output makes painful. Requests and outbox dispatches are enriched with `TenantId` (and `EventId` for events) through logging scopes; a module-name property can be added the same way. |
| API docs | **Microsoft.AspNetCore.OpenApi** (built-in .NET 10) + **Scalar.AspNetCore** | .NET 9+ ships native OpenAPI document generation for Minimal APIs (`AddOpenApi`/`MapOpenApi`) without Swashbuckle's reflection-heavy startup cost; Scalar is a modern, actively developed UI for that document. Swashbuckle remains an option if your team already standardizes on it, but it lagged .NET 9/10's Minimal API + native OpenAPI support at time of writing. |

`Directory.Packages.props` (Central Package Management) pins every package
version once, solution-wide — see the root of this repo. Every `.csproj`'s
`<PackageReference>` omits `Version`; the version lives only in
`Directory.Packages.props`.

---

## 8. Where to look in the code

| What | Where |
|---|---|
| Module composition contract | [`src/BuildingBlocks/Modules/IModule.cs`](src/BuildingBlocks/Modules/IModule.cs) |
| Composition root + platform wiring | [`src/Host/Program.cs`](src/Host/Program.cs), [`src/Host/Setup/`](src/Host/Setup/) |
| Outbox contracts + generic processor | [`src/BuildingBlocks/Outbox/`](src/BuildingBlocks/Outbox/) |
| Module DbContext base (schema, outbox, tenancy) | [`src/BuildingBlocks.Persistence/Persistence/ModuleDbContext.cs`](src/BuildingBlocks.Persistence/Persistence/ModuleDbContext.cs) |
| Result type → problem details | [`src/BuildingBlocks/Results/`](src/BuildingBlocks/Results/), [`src/BuildingBlocks/Web/ResultHttpExtensions.cs`](src/BuildingBlocks/Web/ResultHttpExtensions.cs) |
| Tenant resolution | [`src/BuildingBlocks/MultiTenancy/`](src/BuildingBlocks/MultiTenancy/) |
| Sign-up, login, invitations, plans | [`src/Modules/Identity/`](src/Modules/Identity/) |
| Sales' PlaceOrder vertical slice (sync + async) | [`src/Modules/Sales/Sales.Application/Features/PlaceOrder/`](src/Modules/Sales/Sales.Application/Features/PlaceOrder/) |
| All-or-nothing stock reservation | [`src/Modules/Inventory/Inventory.Domain/StockReservation.cs`](src/Modules/Inventory/Inventory.Domain/StockReservation.cs) |
| Read model built from events | [`src/Modules/Reporting/`](src/Modules/Reporting/) |
| Idempotent migration scripts | [`scripts/migrations/`](scripts/migrations/), generated by [`scripts/generate-migrations.ps1`](scripts/generate-migrations.ps1) |
| Boundary checks on compiled assemblies | [`tests/Architecture.Tests/ModuleBoundaryTests.cs`](tests/Architecture.Tests/ModuleBoundaryTests.cs) |
| End-to-end saga, tenancy, and RBAC proofs | [`tests/Integration.Tests/`](tests/Integration.Tests/) |

See [README.md](README.md) for how to run it. Adding a module means: five
projects in the same shape as `Catalog.*`, one `IModule` implementation, and
one line in `Program.cs`'s `modules` array (the `add-module` Claude skill has
the full checklist).

---

## 9. SaaS concerns

### Tenancy model

**Shared database, shared schema per module, row-level tenant isolation.**
This is the most cost-effective model for many small tenants. Every
tenant-owned table carries a `TenantId` column. Isolation is enforced by
infrastructure, not by developer discipline:

- **Where the tenant comes from.** It comes from the signed `tenant_id` claim
  on the access token, and nowhere else. `TenantResolutionMiddleware` copies
  it into the scoped `ITenantContext`. A header or route value can't change
  it, so one tenant can't pose as another.
- **Automatic filtering.** Every aggregate root configured with
  `builder.IsTenantScoped()` gets a `TenantId` *shadow* property, so the
  domain model never sees it. It also gets a global query filter
  `TenantId == current tenant`. With no tenant in scope, queries return
  nothing: the filter fails closed.
- **Automatic stamping.** `ModuleDbContext.SaveChanges` stamps the current
  tenant on every new tenant-scoped row, and throws if there is none.
- **Asynchronous work.** Every `IIntegrationEvent` carries `TenantId`. The
  outbox processor restores it into the handler's scope before dispatch.
- **Uniqueness.** Indexes that must be unique per tenant lead with `TenantId`
  (`(TenantId, Sku)` for products and stock), so two tenants can reuse a SKU.

Identity is the deliberate exception. Sign-in has to find a user *before* any
tenant is known, so its `tenants` and `users` tables aren't filtered, and every
tenant-specific Identity query takes the tenant id explicitly. Email addresses
are unique platform-wide so that login needs only email and password.

**Upgrade paths.** Tenancy is resolved in exactly one place and filtered in
exactly one base class, so moving a large tenant to a *database-per-tenant*
model later is a change to connection-string resolution in
`AddModuleDbContext`. No handler changes. PostgreSQL row-level security can be
layered underneath as defence in depth by setting `app.tenant_id` per
connection.

### Identity, roles and plans

- Self-service sign-up (`POST /api/identity/tenants/register`) creates a
  tenant on the **Free** plan plus its **Owner**, and returns a token.
- Owners and Admins invite users (`POST /api/identity/users/invitations`).
  The one-time token is emailed by Notifications, and only its SHA-256 hash is
  stored. Invitations expire after 7 days.
- Passwords use ASP.NET Core Identity's PBKDF2 hasher, used standalone. Login
  returns the same error for unknown email and wrong password.
- Three built-in roles (`Owner ⊇ Admin ⊇ Member`) map to module-declared
  permissions (section 6).
- **Plans** (`Free`, `Pro`, `Enterprise`) set usage limits: users are enforced
  by Identity, products by Catalog through Identity's `ITenantPlanProvider`
  contract. `PUT /api/identity/tenant/plan` stands in for a billing-provider
  webhook.

### Platform safeguards (Host)

| Concern | Implementation |
|---|---|
| Errors | Handlers return `Result`. `ToHttpResult()` maps `ErrorType` to RFC 9457 problem details with a stable `code`. `GlobalExceptionHandler` maps validation → 400, optimistic-concurrency/unique conflicts → 409, and everything else → a 500 that is logged but hides details from the client. |
| Rate limiting | A per-IP fixed window on anonymous credential endpoints (brute force), plus a per-tenant global limit (noisy neighbours). Both are configurable under `RateLimiting`. |
| Concurrency | Stock rows use PostgreSQL `xmin` as a row version. Reservations and adjustments can't overwrite each other. |
| Health | `/health/live` (process) and `/health/ready` (every module's database), for orchestrator probes. |
| Configuration | `Auth` settings are validated at startup. Secrets come from user-secrets or environment variables, never `appsettings.json`. |
| Observability | Serilog request logging, with `TenantId`/`EventId` in log scopes. |
| API docs | OpenAPI document with a bearer security scheme, plus the Scalar UI (Development). |

### Deliberately out of scope

These are the usual next steps for a production launch. The design leaves room
for each of them, but they aren't implemented:

- refresh tokens and token revocation, or an external IdP;
- a real email provider (replace `LoggingEmailSender`);
- a real billing integration behind `ChangePlan`;
- multiple outbox workers (`FOR UPDATE SKIP LOCKED`) or a message broker;
- tenant deletion and data export (GDPR);
- distributed tracing and metrics (OpenTelemetry).
