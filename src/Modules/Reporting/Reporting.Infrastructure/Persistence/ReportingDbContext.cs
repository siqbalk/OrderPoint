using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Abstractions;
using Reporting.Domain;

namespace Reporting.Infrastructure.Persistence;

/// <summary>Reporting owns the "reporting" schema exclusively; its read model is tenant-scoped.</summary>
public sealed class ReportingDbContext(DbContextOptions<ReportingDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext), IReportingDbContext
{
    public const string SchemaName = "reporting";

    protected override string Schema => SchemaName;

    public DbSet<SalesRecord> SalesRecords => Set<SalesRecord>();

    public void AddSalesRecord(SalesRecord record) => SalesRecords.Add(record);

    public Task<SalesRecord?> FindSalesRecordAsync(Guid orderId, CancellationToken cancellationToken)
        => SalesRecords.FirstOrDefaultAsync(r => r.OrderId == orderId, cancellationToken);

    public async Task<IReadOnlyList<DailySalesRow>> GetDailySalesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
        => await SalesRecords
            .AsNoTracking()
            .Where(r => r.ConfirmedOnDate >= from && r.ConfirmedOnDate <= to)
            .GroupBy(r => r.ConfirmedOnDate)
            .OrderBy(g => g.Key)
            .Select(g => new DailySalesRow(
                g.Key,
                g.Count(),
                g.Sum(r => r.IsCancelled ? 1 : 0),
                g.Sum(r => r.IsCancelled ? 0m : r.Total),
                g.Sum(r => r.IsCancelled ? 0 : r.ItemCount)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TopOrderRow>> GetTopOrdersAsync(DateOnly from, DateOnly to, int limit, CancellationToken cancellationToken)
        => await SalesRecords
            .AsNoTracking()
            .Where(r => !r.IsCancelled && r.ConfirmedOnDate >= from && r.ConfirmedOnDate <= to)
            .OrderByDescending(r => r.Total)
            .ThenByDescending(r => r.ConfirmedOnUtc)
            .ThenBy(r => r.OrderId)
            .Take(limit)
            .Select(r => new TopOrderRow(r.OrderId, r.ConfirmedOnUtc, r.Total, r.ItemCount))
            .ToListAsync(cancellationToken);
}
