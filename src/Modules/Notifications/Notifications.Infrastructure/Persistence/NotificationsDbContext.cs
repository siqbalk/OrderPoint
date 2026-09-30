using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Pagination;
using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Notifications.Application.Abstractions;
using Notifications.Domain;

namespace Notifications.Infrastructure.Persistence;

/// <summary>Notifications owns the "notifications" schema exclusively; the notification log is tenant-scoped.</summary>
public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext), INotificationsDbContext
{
    public const string SchemaName = "notifications";

    protected override string Schema => SchemaName;

    public DbSet<Notification> Notifications => Set<Notification>();

    public void AddNotification(Notification notification) => Notifications.Add(notification);

    public Task<bool> ExistsAsync(Guid sourceEventId, string recipient, CancellationToken cancellationToken)
        => Notifications.AnyAsync(n => n.SourceEventId == sourceEventId && n.Recipient == recipient, cancellationToken);

    public async Task<PagedResult<Notification>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = Notifications.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(n => n.SentOnUtc)
            .ThenBy(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Notification>(items, page, pageSize, total);
    }
}
