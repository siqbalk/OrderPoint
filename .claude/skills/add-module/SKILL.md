---
name: add-module
description: Scaffold a brand-new business module (e.g. Billing, Shipping) with the standard five projects — Domain, Application, Infrastructure, Contracts, Endpoints — its own PostgreSQL schema and migration history, IModule registration, permissions, and solution/script wiring. Use when asked to create or add a new module.
argument-hint: <ModuleName>
disable-model-invocation: true
---

# Scaffold a new module

Argument: `$ARGUMENTS` — the module name in PascalCase (e.g. `Billing`). Its schema and permission prefix are the lowercase form (`billing`). Confirm the name and the first feature it should have before generating anything.

Catalog is the smallest complete template: mirror `src/Modules/Catalog/` file for file, renaming `Catalog` → `{Module}` and `catalog` → `{module}`. Don't copy Catalog's business types (Product, CreateProduct…); create one placeholder aggregate and one feature the user agreed on. Project properties (TargetFramework, Nullable…) come from `Directory.Build.props` — don't repeat them.

## Steps

1. **Projects** under `src/Modules/{Module}/` — copy each `.csproj` from Catalog, keeping the same `<ProjectReference>` shape, and drop the `Identity.Contracts` reference unless the module really needs it. `<PackageReference>` elements have no `Version` (Central Package Management).
   - `{Module}.Domain` → BuildingBlocks only.
   - `{Module}.Contracts` → BuildingBlocks only. Holds `{Module}Permissions`, integration events, and sync interfaces.
   - `{Module}.Application` → own Domain, own Contracts, BuildingBlocks (+ other modules' `*.Contracts` only if needed). No EF Core packages, no BuildingBlocks.Persistence.
   - `{Module}.Infrastructure` → own Application, Domain, Contracts, BuildingBlocks, BuildingBlocks.Persistence; EF Core + Npgsql packages.
   - `{Module}.Endpoints` → own Application, Infrastructure, Contracts, BuildingBlocks, BuildingBlocks.Persistence.
2. **Persistence** in `{Module}.Infrastructure/Persistence/`:
   - `{Module}DbContext : ModuleDbContext, I{Module}DbContext` with `public const string SchemaName = "{module}";` and `protected override string Schema => SchemaName;`. The base class supplies the schema, the outbox table, `IOutboxWriter`, and tenant filtering/stamping.
   - `Configurations/` — one `IEntityTypeConfiguration<T>` per aggregate. Call `builder.IsTenantScoped()` on every tenant-owned root, and put `ModuleDbContext.TenantIdProperty` first in composite/unique indexes.
   - `{Module}DbContextFactory` (design-time): `new(ModulePersistenceExtensions.DesignTimeOptions<{Module}DbContext>({Module}DbContext.SchemaName), ModulePersistenceExtensions.DesignTimeTenant)`.
3. **Application** — `Abstractions/I{Module}DbContext.cs` plus the first feature (follow the `add-feature` skill).
4. **Endpoints** — `{Module}Module : IModule` copied from `CatalogModule.cs`:
   - `Permissions` list of `PermissionDefinition`s named `{module}:{resource}:{action}`. The default `AddAuthorizationPolicies` turns each one into a policy.
   - `AddModule`: `services.AddModuleApplication(typeof(I{Module}DbContext).Assembly)`, `services.AddModuleDbContext<{Module}DbContext>(configuration, Name, {Module}DbContext.SchemaName)` (registers the DbContext with the schema-scoped history table, the outbox store, and a health check), the `I{Module}DbContext` mapping, and `RegisterEventsFromAssembly` if the module publishes events.
   - `MapEndpoints`: `endpoints.MapGroup("/api/{module}").WithTags("{Module}")`, then one `Map{Feature}()` per feature.
5. **Wire up** (these are the only edits outside the module):
   - `OrderPoint.slnx`: a `/src/Modules/{Module}/` folder with the five projects.
   - `src/Host/Host.csproj`: one reference to `{Module}.Endpoints`.
   - `src/Host/Program.cs`: add `new {Module}Module()` to the `IModule[] modules` array. Nothing else in Program.cs changes.
   - `scripts/generate-migrations.ps1`: add the module to `$modules`.
   - `.github/workflows/ci.yml`: add the module to the `has-pending-model-changes` loop.
   - `tests/Architecture.Tests/ModuleBoundaryTests.cs`: add the module to `Modules`.
6. **Initial migration** — run the `add-migration` skill with `InitialCreate`, which also regenerates `scripts/migrations/{module}.sql`.
7. **Tests** — ask whether to create `tests/{Module}.Tests` (copy `Catalog.Tests.csproj`) and add a handler test for the first feature.
8. **Verify** — `dotnet build`, `dotnet test tests/Architecture.Tests`, `dotnet test tests/{Module}.Tests` (if created), `dotnet run .claude/hooks/guard.cs -- --scan`. Report the tree of files created.
