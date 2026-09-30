<#
.SYNOPSIS
Regenerates the idempotent SQL migration scripts for every module, for use by
the CI/CD deploy step. Run this locally after adding a new EF Core migration
to any module, and commit the resulting .sql file alongside the migration.

Each module's script is independent: applying sales.sql does not require
inventory.sql to run first or vice versa, and each is safe to re-run (it
only applies migrations not already recorded in that module's own
__ef_migrations_history table).
#>

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $root "scripts/migrations"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$modules = @(
    @{ Name = "sales";     Project = "src/Modules/Sales/Sales.Infrastructure";         Context = "Sales.Infrastructure.Persistence.SalesDbContext" }
    @{ Name = "inventory"; Project = "src/Modules/Inventory/Inventory.Infrastructure"; Context = "Inventory.Infrastructure.Persistence.InventoryDbContext" }
    @{ Name = "identity";      Project = "src/Modules/Identity/Identity.Infrastructure";           Context = "Identity.Infrastructure.Persistence.IdentityDbContext" }
    @{ Name = "catalog";       Project = "src/Modules/Catalog/Catalog.Infrastructure";             Context = "Catalog.Infrastructure.Persistence.CatalogDbContext" }
    @{ Name = "notifications"; Project = "src/Modules/Notifications/Notifications.Infrastructure"; Context = "Notifications.Infrastructure.Persistence.NotificationsDbContext" }
    @{ Name = "reporting";     Project = "src/Modules/Reporting/Reporting.Infrastructure";         Context = "Reporting.Infrastructure.Persistence.ReportingDbContext" }
)

foreach ($module in $modules) {
    $projectPath = Join-Path $root $module.Project
    $outputPath = Join-Path $outputDir "$($module.Name).sql"

    Write-Host "Generating idempotent script for $($module.Name) -> $outputPath"

    dotnet ef migrations script --idempotent `
        --project $projectPath `
        --startup-project $projectPath `
        --context $module.Context `
        --output $outputPath

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to generate migration script for module '$($module.Name)'"
    }
}

Write-Host "Done. Apply each script with: psql -f scripts/migrations/<module>.sql `$env:DATABASE_URL"
