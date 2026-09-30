using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace Sales.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations add` run against Sales.Infrastructure directly
/// (see scripts/) without needing to spin up the full Host at design time.
/// Uses a fixed design-time connection string — never the runtime one.
/// </summary>
public sealed class SalesDbContextFactory : IDesignTimeDbContextFactory<SalesDbContext>
{
    public SalesDbContext CreateDbContext(string[] args)
        => new(ModulePersistenceExtensions.DesignTimeOptions<SalesDbContext>(SalesDbContext.SchemaName),
            ModulePersistenceExtensions.DesignTimeTenant);
}
