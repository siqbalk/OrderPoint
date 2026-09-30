---
name: add-integration-event
description: Add an asynchronous cross-module integration event using the transactional outbox — event record in the publisher's Contracts, enqueue in the publisher's handler, and an idempotent INotificationHandler in the consumer. Use when one module must react to something that happened in another module.
argument-hint: <PublisherModule> <EventName> <ConsumerModule>
---

# Add a cross-module integration event (outbox)

Arguments: `$ARGUMENTS` — publishing module, event name ending in `IntegrationEvent` (e.g. `OrderCancelledIntegrationEvent`), and consuming module. Ask for anything missing.

Reference flow to copy: Sales publishes `OrderPlacedIntegrationEvent` → Inventory handles it.

- Event: `src/Modules/Sales/Sales.Contracts/IntegrationEvents/OrderPlacedIntegrationEvent.cs`
- Enqueue: `src/Modules/Sales/Sales.Application/Features/PlaceOrder/PlaceOrderHandler.cs`
- Type map: `SalesModule.AddModule` (`o.RegisterEventsFromAssembly(typeof(OrderPlacedIntegrationEvent).Assembly)`), which registers every event in the Contracts assembly
- Consumer: `src/Modules/Inventory/Inventory.Application/Features/ReserveStockOnOrderPlaced/ReserveStockOnOrderPlacedHandler.cs`

If the caller needs an answer *now* (a query, not a notification), this is the wrong tool: publish a narrow interface in the implementing module's `Contracts` instead (see `IInventoryAvailabilityChecker`).

## Steps

1. **Event contract** — `src/Modules/{Publisher}/{Publisher}.Contracts/IntegrationEvents/{EventName}.cs`: `sealed record` implementing `IIntegrationEvent`, starting with `Guid EventId, DateTimeOffset OccurredOnUtc, Guid TenantId`, then only primitives/IDs/contract records the consumer needs. No domain types. It's a public contract: add fields, never rename or remove them.
2. **Publish** — in the publisher's command handler, call `dbContext.Outbox.Enqueue(new {EventName}(Guid.NewGuid(), now, tenantContext.RequiredTenantId, ...))` *before* the single `SaveChangesAsync`, so the business change and outbox row commit in one local transaction. Every module's DbContext derives from `ModuleDbContext` (which implements `IOutboxWriter`). If `I{Publisher}DbContext` has no `Outbox` property yet, add one the way `ISalesDbContext` does.
3. **Register the type** — if this is the publisher's first event, add `services.Configure<OutboxProcessorOptions>(o => o.RegisterEventsFromAssembly(typeof({EventName}).Assembly));` to `{Publisher}Module.AddModule`. Later events in the same Contracts assembly are picked up automatically. Host never changes.
4. **Consumer reference** — the consumer's `*.Application.csproj` references `..\..\{Publisher}\{Publisher}.Contracts\{Publisher}.Contracts.csproj` if it doesn't already. Only Contracts — never the publisher's other projects, and the publisher never references the consumer.
5. **Consumer handler** — `src/Modules/{Consumer}/{Consumer}.Application/Features/{ReactionName}On{Event}/…Handler.cs`: `INotificationHandler<{EventName}>`. The OutboxProcessor restores `event.TenantId` into the handler's scope, so tenant query filters apply normally. Make it idempotent, because the outbox is at-least-once and a whole message is retried when any one handler throws. Key on a natural id (OrderId, EventId) with a unique index, tolerate a missing entity or an already-applied change, and return rather than throw. Throw only when a retry could succeed (transient failure).
6. **Test** — unit test the consumer handler with NSubstitute, including a redelivery case. If the flow matters end-to-end, extend `tests/Integration.Tests/OrderLifecycleTests.cs` (needs Docker) and use `Api.EventuallyAsync` to wait for delivery.
7. **Verify** — `dotnet build`, `dotnet test`, then `dotnet run .claude/hooks/guard.cs -- --scan`.

Never share a `DbTransaction`/`TransactionScope` across modules. For multi-step processes that need compensation, model a saga (DESIGN.md section 4).
