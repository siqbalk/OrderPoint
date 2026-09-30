using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Outbox;
using BuildingBlocks.Pagination;
using BuildingBlocks.Persistence;
using Inventory.Application.Abstractions;
using Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Persistence;

/// <summary>
/// Inventory owns the "inventory" schema exclusively — mirrors SalesDbContext's isolation.
/// </summary>
public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext), IInventoryDbContext
{
    public const string SchemaName = "inventory";

    protected override string Schema => SchemaName;

    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();

    IOutboxWriter IInventoryDbContext.Outbox => this;

    public void AddStockItem(StockItem stockItem) => StockItems.Add(stockItem);

    public void AddReservation(StockReservation reservation) => StockReservations.Add(reservation);

    public Task<StockItem?> FindBySkuAsync(string normalizedSku, CancellationToken cancellationToken)
        => StockItems.FirstOrDefaultAsync(s => s.Sku == normalizedSku, cancellationToken);

    public async Task<StockItem> GetOrCreateBySkuAsync(string normalizedSku, CancellationToken cancellationToken)
    {
        if (await FindBySkuAsync(normalizedSku, cancellationToken) is { } existing)
        {
            return existing;
        }

        // Atomic upsert-if-missing; the unique (TenantId, Sku) index arbitrates concurrent callers.
        var seed = StockItem.Create(normalizedSku, 0, DateTimeOffset.UtcNow);
        await Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO inventory.stock_items ("Id", "Sku", "QuantityOnHand", "QuantityReserved", "UpdatedOnUtc", "TenantId")
            VALUES ({seed.Id}, {seed.Sku}, 0, 0, {seed.UpdatedOnUtc}, {RequiredTenantId})
            ON CONFLICT ("TenantId", "Sku") DO NOTHING
            """, cancellationToken);

        return await FindBySkuAsync(normalizedSku, cancellationToken)
            ?? throw new InvalidOperationException($"Stock item '{normalizedSku}' was not created.");
    }

    public Task<Dictionary<string, StockItem>> FindBySkusAsync(IReadOnlyCollection<string> normalizedSkus, CancellationToken cancellationToken)
        => StockItems
            .Where(s => normalizedSkus.Contains(s.Sku))
            .ToDictionaryAsync(s => s.Sku, cancellationToken);

    public async Task<PagedResult<StockItem>> ListAsync(int? maxAvailable, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = StockItems.AsNoTracking();
        if (maxAvailable is { } max)
        {
            query = query.Where(s => s.QuantityOnHand - s.QuantityReserved <= max);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(s => s.Sku)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StockItem>(items, page, pageSize, total);
    }

    public Task<StockReservation?> FindReservationByOrderAsync(Guid orderId, CancellationToken cancellationToken)
        => StockReservations.FirstOrDefaultAsync(r => r.OrderId == orderId, cancellationToken);
}
