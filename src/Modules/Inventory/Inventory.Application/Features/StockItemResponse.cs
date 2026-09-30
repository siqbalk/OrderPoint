using BuildingBlocks.Results;
using Inventory.Domain;

namespace Inventory.Application.Features;

public sealed record StockItemResponse(
    Guid Id,
    string Sku,
    int QuantityOnHand,
    int QuantityReserved,
    int QuantityAvailable,
    DateTimeOffset UpdatedOnUtc)
{
    public static StockItemResponse From(StockItem s)
        => new(s.Id, s.Sku, s.QuantityOnHand, s.QuantityReserved, s.QuantityAvailable, s.UpdatedOnUtc);
}

internal static class InventoryErrors
{
    public static Error StockNotFound(string sku)
        => Error.NotFound("Stock.NotFound", $"No stock item found for SKU '{sku}'.");
}
