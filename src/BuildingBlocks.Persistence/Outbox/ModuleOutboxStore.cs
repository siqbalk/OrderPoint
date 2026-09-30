using BuildingBlocks.Persistence;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Persistence.Outbox;

/// <summary>
/// The <see cref="IOutboxStore"/> for one module, reading that module's own
/// <c>{schema}.outbox_messages</c> table through its own DbContext.
/// </summary>
public sealed class ModuleOutboxStore<TContext>(TContext dbContext, string moduleName) : IOutboxStore
    where TContext : ModuleDbContext
{
    private const int MaxErrorLength = 2000;

    public string ModuleName => moduleName;

    public async Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(int batchSize, int maxAttempts, CancellationToken cancellationToken)
        => await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(m => m.ProcessedOnUtc == null && m.Attempts < maxAttempts)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken)
        => dbContext.OutboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.ProcessedOnUtc, DateTimeOffset.UtcNow)
                .SetProperty(m => m.Error, (string?)null), cancellationToken);

    public Task MarkFailedAsync(Guid messageId, string error, CancellationToken cancellationToken)
    {
        var truncated = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
        return dbContext.OutboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Attempts, m => m.Attempts + 1)
                .SetProperty(m => m.Error, truncated), cancellationToken);
    }
}
