using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace Notifications.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef</c> target Notifications.Infrastructure directly, without the Host.</summary>
public sealed class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
        => new(ModulePersistenceExtensions.DesignTimeOptions<NotificationsDbContext>(NotificationsDbContext.SchemaName),
            ModulePersistenceExtensions.DesignTimeTenant);
}
