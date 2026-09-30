using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace Identity.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef</c> target Identity.Infrastructure directly, without the Host.</summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
        => new(ModulePersistenceExtensions.DesignTimeOptions<IdentityDbContext>(IdentityDbContext.SchemaName),
            ModulePersistenceExtensions.DesignTimeTenant);
}
