---
name: check-architecture
description: Audit the solution for modular-monolith boundary violations — cross-module references that bypass Contracts, EF Core in Application, framework code in Domain, module code in BuildingBlocks/Host, schema leaks, versioned PackageReferences. Use before a PR, after a refactor, or when asked whether the module boundaries are clean.
---

# Check module boundaries

The compiler doesn't enforce module isolation here; this audit does.

## Steps

1. Run the rule scan (same rules as the PreToolUse hook, over every file):
   ```
   dotnet run .claude/hooks/guard.cs -- --scan
   ```
   The first run compiles the script and can take a minute or two; later runs take under a second.
2. Cross-check real project references for every module project, since the scan reads text and can't see transitive references:
   ```
   dotnet list src/Modules/{Module}/{Module}.{Layer}/{Module}.{Layer}.csproj reference
   ```
   Allowed: own-module layers per CLAUDE.md, `BuildingBlocks`, and other modules' `*.Contracts` only. Domain and Contracts reference only BuildingBlocks. A publisher must never reference a consumer's project (e.g. `Sales.*` → `Inventory.Application`).
3. Look for what text rules miss:
   - Fully-qualified cross-module types without a `using` (grep `src/Modules` for `{OtherModule}\.(Domain|Application|Infrastructure)\.`).
   - New modules added under `src/Modules` but missing from `Program.cs`'s `IModule[]`, `OrderPoint.slnx`, or `scripts/generate-migrations.ps1`.
   - Business concepts (Order, Stock, Customer…) that have crept into `src/BuildingBlocks`.
4. Report violations grouped by module, each with file path, the rule it breaks (cite CLAUDE.md/DESIGN.md), and the fix. Don't change code unless asked.
