using Reporting.Domain;

namespace Reporting.Application.Abstractions;

public interface IReportingDbContext
{
    void AddSalesRecord(SalesRecord record);

    Task<SalesRecord?> FindSalesRecordAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>One row per UTC day in [from, to] that has at least one confirmed order.</summary>
    Task<IReadOnlyList<DailySalesRow>> GetDailySalesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>The highest-value orders confirmed in [from, to] and not since cancelled, largest first.</summary>
    Task<IReadOnlyList<TopOrderRow>> GetTopOrdersAsync(DateOnly from, DateOnly to, int limit, CancellationToken cancellationToken);

    /// <summary>One row per UTC month of <paramref name="year"/> that has at least one confirmed order.</summary>
    Task<IReadOnlyList<MonthlySalesRow>> GetMonthlySalesAsync(int year, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <param name="Revenue">Total of confirmed orders that have not since been cancelled.</param>
public sealed record DailySalesRow(DateOnly Date, int ConfirmedOrders, int CancelledOrders, decimal Revenue, int ItemsSold);

/// <param name="Month">1 = January.</param>
/// <param name="Revenue">Total of confirmed orders that have not since been cancelled.</param>
public sealed record MonthlySalesRow(int Month, int ConfirmedOrders, int CancelledOrders, decimal Revenue, int ItemsSold);

public sealed record TopOrderRow(Guid OrderId, DateTimeOffset ConfirmedOnUtc, decimal Total, int ItemCount);
