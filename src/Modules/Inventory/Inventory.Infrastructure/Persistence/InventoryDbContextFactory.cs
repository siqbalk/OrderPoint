using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace Inventory.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef</c> target Inventory.Infrastructure directly, without the Host.</summary>
public sealed class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
        => new(ModulePersistenceExtensions.DesignTimeOptions<InventoryDbContext>(InventoryDbContext.SchemaName),
            ModulePersistenceExtensions.DesignTimeTenant);
}
