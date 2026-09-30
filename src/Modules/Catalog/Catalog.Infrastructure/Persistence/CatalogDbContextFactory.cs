using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace Catalog.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef</c> target Catalog.Infrastructure directly, without the Host.</summary>
public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
        => new(ModulePersistenceExtensions.DesignTimeOptions<CatalogDbContext>(CatalogDbContext.SchemaName),
            ModulePersistenceExtensions.DesignTimeTenant);
}
