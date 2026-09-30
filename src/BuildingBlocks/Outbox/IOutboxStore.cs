namespace BuildingBlocks.Outbox;

/// <summary>
/// Implemented once per module (by that module's Infrastructure layer) so the
/// generic <see cref="OutboxProcessor"/> in Host can poll every module's outbox
/// table without knowing anything about that module's DbContext or schema.
/// </summary>
public interface IOutboxStore
{
    string ModuleName { get; }

    /// <summary>Oldest unprocessed messages that have failed fewer than <paramref name="maxAttempts"/> times.</summary>
    Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(int batchSize, int maxAttempts, CancellationToken cancellationToken);

    Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken);

    /// <summary>Records the error and increments the attempt counter.</summary>
    Task MarkFailedAsync(Guid messageId, string error, CancellationToken cancellationToken);
}
