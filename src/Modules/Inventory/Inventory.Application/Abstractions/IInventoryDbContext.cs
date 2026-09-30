using BuildingBlocks.Outbox;
using BuildingBlocks.Pagination;
using Inventory.Domain;

namespace Inventory.Application.Abstractions;

/// <summary>All reads are automatically limited to the current tenant by the DbContext's query filter.</summary>
public interface IInventoryDbContext
{
    void AddStockItem(StockItem stockItem);

    void AddReservation(StockReservation reservation);

    Task<StockItem?> FindBySkuAsync(string normalizedSku, CancellationToken cancellationToken);

    /// <summary>
    /// Loads the SKU's stock record, first creating an empty one if none exists.
    /// Safe under concurrency: a product being created and its first delivery
    /// being received at the same moment both end up with the same single record.
    /// </summary>
    Task<StockItem> GetOrCreateBySkuAsync(string normalizedSku, CancellationToken cancellationToken);

    /// <summary>Stock items for the given SKUs, keyed by normalised SKU, loaded for update.</summary>
    Task<Dictionary<string, StockItem>> FindBySkusAsync(IReadOnlyCollection<string> normalizedSkus, CancellationToken cancellationToken);

    Task<PagedResult<StockItem>> ListAsync(int? maxAvailable, int page, int pageSize, CancellationToken cancellationToken);

    Task<StockReservation?> FindReservationByOrderAsync(Guid orderId, CancellationToken cancellationToken);

    IOutboxWriter Outbox { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
