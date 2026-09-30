using BuildingBlocks.Results;

namespace Inventory.Domain;

/// <summary>Stock level of one SKU in one tenant's warehouse.</summary>
public sealed class StockItem
{
    private StockItem() { }

    public Guid Id { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public int QuantityOnHand { get; private set; }
    public int QuantityReserved { get; private set; }
    public DateTimeOffset UpdatedOnUtc { get; private set; }

    public int QuantityAvailable => QuantityOnHand - QuantityReserved;

    public static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();

    public static StockItem Create(string sku, int quantityOnHand, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("SKU is required.", nameof(sku));
        if (quantityOnHand < 0)
            throw new ArgumentOutOfRangeException(nameof(quantityOnHand), "Quantity on hand cannot be negative.");

        return new StockItem
        {
            Id = Guid.NewGuid(),
            Sku = NormalizeSku(sku),
            QuantityOnHand = quantityOnHand,
            QuantityReserved = 0,
            UpdatedOnUtc = now,
        };
    }

    /// <summary>Goods received into the warehouse.</summary>
    public void Receive(int quantity, DateTimeOffset now)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");

        QuantityOnHand += quantity;
        UpdatedOnUtc = now;
    }

    /// <summary>Stocktake correction: sets on-hand to the counted quantity.</summary>
    public Result AdjustTo(int countedQuantity, DateTimeOffset now)
    {
        if (countedQuantity < 0)
            return Result.Failure(Error.Validation("Stock.NegativeQuantity", "Quantity on hand cannot be negative."));
        if (countedQuantity < QuantityReserved)
            return Result.Failure(Error.Conflict(
                "Stock.BelowReserved",
                $"SKU '{Sku}' has {QuantityReserved} units reserved for open orders; on-hand cannot go below that."));

        QuantityOnHand = countedQuantity;
        UpdatedOnUtc = now;
        return Result.Success();
    }

    public void Reserve(int quantity, DateTimeOffset now)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        if (quantity > QuantityAvailable)
            throw new InvalidOperationException($"Cannot reserve {quantity} of SKU '{Sku}'; only {QuantityAvailable} available.");

        QuantityReserved += quantity;
        UpdatedOnUtc = now;
    }

    public void Release(int quantity, DateTimeOffset now)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");

        QuantityReserved = Math.Max(0, QuantityReserved - quantity);
        UpdatedOnUtc = now;
    }
}
