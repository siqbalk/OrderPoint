using Reporting.Domain;

namespace Reporting.Application.Abstractions;

public interface IReportingDbContext
{
    void AddSalesRecord(SalesRecord record);

    Task<SalesRecord?> FindSalesRecordAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>One row per UTC day in [from, to] that has at least one confirmed order.</summary>
    Task<IReadOnlyList<DailySalesRow>> GetDailySalesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <param name="Revenue">Total of confirmed orders that have not since been cancelled.</param>
public sealed record DailySalesRow(DateOnly Date, int ConfirmedOrders, int CancelledOrders, decimal Revenue, int ItemsSold);
