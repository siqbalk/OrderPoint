---
name: add-feature
description: Add a new vertical-slice feature (CQRS command or query) to an existing module — Application command/query + validator + handler, Minimal API endpoint, module registration, authorization policy, and a unit test. Use when asked to add an endpoint, use case, command, or query to Sales, Inventory, or any other module under src/Modules.
argument-hint: <Module> <FeatureName> <command|query>
---

# Add a vertical-slice feature

Arguments: `$ARGUMENTS` — module name, feature name (PascalCase verb phrase, e.g. `CancelOrder`), and whether it's a `command` (changes state) or `query` (reads only). Ask for anything missing.

Read the reference slice first and copy its shape exactly — don't invent a new style:

- Command reference: `src/Modules/Inventory/Inventory.Application/Features/AddStock/` + `src/Modules/Inventory/Inventory.Endpoints/Features/AddStock/AddStockEndpoint.cs`
- Query reference: `src/Modules/Inventory/Inventory.Application/Features/GetStockBySku/` + `src/Modules/Inventory/Inventory.Endpoints/Features/GetStockBySku/GetStockBySkuEndpoint.cs`
- Cross-module sync call + outbox reference: `src/Modules/Sales/Sales.Application/Features/PlaceOrder/PlaceOrderHandler.cs`

## Steps

1. **Application** — `src/Modules/{Module}/{Module}.Application/Features/{Feature}/`
   - `{Feature}Command.cs` or `{Feature}Query.cs`: a `sealed record` implementing `IRequest<Result<{Feature}Response>>`, with the `{Feature}Response` record in the same file.
   - `{Feature}Validator.cs`: `AbstractValidator<T>` (commands always; queries when they take input). `ValidationBehavior` runs it automatically — never call it by hand.
   - `{Feature}Handler.cs`: primary-constructor `sealed class` implementing `IRequestHandler<...>`. Expected failures return `Result.Failure<T>(Error.NotFound|Validation|Conflict(...))` with a `"{Area}.{Reason}"` code — don't throw.
   - Data access only through `I{Module}DbContext`. If you need a new query/add method, add it to `Abstractions/I{Module}DbContext.cs` and implement it in `{Module}.Infrastructure/Persistence/{Module}DbContext.cs`. Never `using Microsoft.EntityFrameworkCore` in Application.
   - Needing another module's data: consume that module's `*.Contracts` interface via constructor injection (sync), or react to its integration event (async — use the `add-integration-event` skill). Never reference its Application/Domain/Infrastructure.
2. **Domain** — put business rules and state changes on the entity (`{Module}.Domain`) as methods, not in the handler. Domain stays framework-free.
   - Tenant isolation is automatic: the module's DbContext filters and stamps `TenantId` on every entity configured with `.IsTenantScoped()`. Never filter by tenant by hand; inject `ITenantContext` only when you need the id itself (e.g. to put it on an integration event). Use the injected `TimeProvider`, not `DateTimeOffset.UtcNow`.
3. **Endpoint** — `src/Modules/{Module}/{Module}.Endpoints/Features/{Feature}/{Feature}Endpoint.cs`: `public static class` with `Map{Feature}(this IEndpointRouteBuilder app)`. Routes are relative to the module's `MapGroup` prefix (`/api/{module-lowercase}/...`). Add `.WithName("{Feature}")`, `.Produces<TResponse>()`, and `.RequireAuthorization({Module}Permissions.X)`. Return `result.ToHttpResult(Results.Ok)` (from `BuildingBlocks.Web`), which maps each `ErrorType` to RFC 9457 problem details. Don't hand-roll error responses.
4. **Module wiring** — in `{Module}.Endpoints/{Module}Module.cs`: call `group.Map{Feature}()` in `MapEndpoints`. If the permission is new, add a constant to `{Module}.Contracts/{Module}Permissions.cs` and a `PermissionDefinition` (with the lowest `TenantRole` that should get it) to the module's `Permissions` list. That one entry creates the authorization policy and puts the claim into tokens for that role. Names are lowercase `{module}:{resource}:{action}` and never reuse another module's prefix. MediatR/validators are assembly-scanned — no per-handler registration.
5. **Schema change?** If you added/changed an entity or configuration, run the `add-migration` skill.
6. **Unit test** — add `{Feature}HandlerTests.cs` to the module's test project (e.g. `tests/Sales.Tests`, following `PlaceOrderHandlerTests.cs`: NSubstitute fakes for `I{Module}DbContext` and any Contracts interfaces, one success path, one failure path). If the module has no test project yet, say so and ask before creating one.
7. **Verify** — `dotnet build` then `dotnet test tests/{Module}.Tests`. Report the results.
