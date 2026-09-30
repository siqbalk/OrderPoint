---
name: add-migration
description: Create an EF Core migration for ONE module's DbContext and regenerate that module's idempotent SQL script. Use after changing a module's entities or IEntityTypeConfiguration classes, or when asked to add/check a migration.
argument-hint: <Module> <MigrationName>
---

# Add a per-module EF Core migration

Arguments: `$ARGUMENTS` — module name (e.g. `Sales`) and a PascalCase migration name (e.g. `AddOrderStatus`). Ask for anything missing.

Migrations are per module, never solution-wide, and never target `src/Host` (DESIGN.md section 3). The PreToolUse hook blocks `dotnet ef` commands that break these rules.

Let `INFRA = src/Modules/{Module}/{Module}.Infrastructure` and `CTX = {Module}.Infrastructure.Persistence.{Module}DbContext`.

## Steps

1. Confirm the tool exists: `dotnet ef --version`. If not, tell the user to run `dotnet tool install --global dotnet-ef` (don't install global tools yourself).
2. Check there's actually a model change:
   ```
   dotnet ef migrations has-pending-model-changes --project INFRA --startup-project INFRA --context CTX
   ```
   If nothing is pending, stop and say so.
3. Add the migration:
   ```
   dotnet ef migrations add {MigrationName} --project INFRA --startup-project INFRA --context CTX --output-dir Persistence/Migrations
   ```
4. Review the generated `Persistence/Migrations/*_{MigrationName}.cs`:
   - Every table/index lives in this module's schema only (`sales`, `inventory`, ...). Anything touching another schema is a bug in the model — fix the configuration, don't hand-edit the migration.
   - Flag destructive operations (`DropColumn`, `DropTable`, `AlterColumn` narrowing types) to the user explicitly.
5. Regenerate SQL scripts: run `./scripts/generate-migrations.ps1` (it covers every module listed in its `$modules` array). If this is a module that isn't in that array yet, add it there.
6. `dotnet build` and report: migration file path, a one-paragraph summary of the schema change, any destructive operations, and the updated `scripts/migrations/{module}.sql`.

Never run `dotnet ef database update` or call `Database.Migrate()` — production schema changes ship as the idempotent SQL scripts.
