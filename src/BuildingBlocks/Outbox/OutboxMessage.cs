namespace BuildingBlocks.Outbox;

/// <summary>
/// Persisted in each module's own schema (module_name.outbox_messages).
/// Written in the same DB transaction as the business change that raised it.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }

    /// <summary>Full CLR type name of the integration event, e.g. <c>Sales.Contracts.IntegrationEvents.OrderPlacedIntegrationEvent</c>.</summary>
    public required string Type { get; init; }

    public required string Content { get; init; }
    public DateTimeOffset OccurredOnUtc { get; init; }
    public DateTimeOffset? ProcessedOnUtc { get; set; }

    /// <summary>Delivery attempts that failed. Messages stop being retried once this reaches <see cref="OutboxProcessorOptions.MaxAttempts"/>.</summary>
    public int Attempts { get; set; }

    public string? Error { get; set; }
}
