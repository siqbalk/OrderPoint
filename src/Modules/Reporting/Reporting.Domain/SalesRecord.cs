namespace Reporting.Domain;

/// <summary>
/// Reporting's own read model of a confirmed order, built entirely from Sales'
/// integration events. Reporting never queries the "sales" schema: if Sales is
/// extracted into its own service, this module keeps working unchanged.
/// Keyed by OrderId, which makes the event handlers idempotent.
/// </summary>
public sealed class SalesRecord
{
    private SalesRecord() { }

    public Guid OrderId { get; private set; }
    public DateTimeOffset ConfirmedOnUtc { get; private set; }

    /// <summary>UTC calendar day of confirmation, stored so daily roll-ups are a plain GROUP BY.</summary>
    public DateOnly ConfirmedOnDate { get; private set; }

    public decimal Total { get; private set; }
    public int ItemCount { get; private set; }
    public bool IsCancelled { get; private set; }
    public DateTimeOffset? CancelledOnUtc { get; private set; }

    public static SalesRecord Confirmed(Guid orderId, DateTimeOffset confirmedOnUtc, decimal total, int itemCount) => new()
    {
        OrderId = orderId,
        ConfirmedOnUtc = confirmedOnUtc,
        ConfirmedOnDate = DateOnly.FromDateTime(confirmedOnUtc.UtcDateTime),
        Total = total,
        ItemCount = itemCount,
    };

    public void Cancel(DateTimeOffset now)
    {
        if (IsCancelled)
            return;

        IsCancelled = true;
        CancelledOnUtc = now;
    }
}
