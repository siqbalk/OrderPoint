using Inventory.Contracts;
using Inventory.Domain;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.InternalServices;

/// <summary>
/// Concrete implementation of Inventory's published synchronous contract.
/// Registered into DI by InventoryModule and consumed by Sales purely through
/// the Inventory.Contracts interface.
/// </summary>
public sealed class InventoryAvailabilityChecker(InventoryDbContext dbContext) : IInventoryAvailabilityChecker
{
    public async Task<IReadOnlyCollection<string>> FindUnavailableAsync(IReadOnlyCollection<StockRequirement> requirements, CancellationToken cancellationToken)
    {
        var required = requirements
            .GroupBy(r => StockItem.NormalizeSku(r.Sku))
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));

        var skus = required.Keys.ToArray();
        var available = await dbContext.StockItems
            .AsNoTracking()
            .Where(s => skus.Contains(s.Sku))
            .ToDictionaryAsync(s => s.Sku, s => s.QuantityOnHand - s.QuantityReserved, cancellationToken);

        return required
            .Where(r => !available.TryGetValue(r.Key, out var quantity) || quantity < r.Value)
            .Select(r => r.Key)
            .ToList();
    }
}
