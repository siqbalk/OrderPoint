using Inventory.Domain;

namespace Inventory.Tests;

public sealed class StockReservationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static Dictionary<string, StockItem> Stock(params (string Sku, int OnHand)[] items)
        => items.ToDictionary(i => i.Sku, i => StockItem.Create(i.Sku, i.OnHand, Now));

    [Fact]
    public void TryReserve_ReservesEveryLine_WhenAllAvailable()
    {
        var stock = Stock(("A", 10), ("B", 5));

        var result = StockReservation.TryReserve(Guid.NewGuid(), [new("A", 3), new("B", 5)], stock, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, stock["A"].QuantityReserved);
        Assert.Equal(0, stock["B"].QuantityAvailable);
    }

    [Fact]
    public void TryReserve_IsAllOrNothing()
    {
        var stock = Stock(("A", 10), ("B", 1));

        var result = StockReservation.TryReserve(Guid.NewGuid(), [new("A", 3), new("B", 2)], stock, Now);

        Assert.True(result.IsFailure);
        Assert.Equal(0, stock["A"].QuantityReserved);
        Assert.Equal(0, stock["B"].QuantityReserved);
        Assert.Equal(["B"], StockReservation.FindShortages([new("A", 3), new("B", 2)], stock));
    }

    [Fact]
    public void TryReserve_TreatsMissingSkusAsUnavailable_AndMergesDuplicateLines()
    {
        var stock = Stock(("A", 4));

        Assert.Equal(["A", "MISSING"], StockReservation.FindShortages([new("a", 3), new("A", 2), new("missing", 1)], stock));
    }

    [Fact]
    public void Release_ReturnsStock_AndIsIdempotent()
    {
        var stock = Stock(("A", 10));
        var reservation = StockReservation.TryReserve(Guid.NewGuid(), [new("A", 4)], stock, Now).Value;

        reservation.Release(stock, Now);
        reservation.Release(stock, Now);

        Assert.Equal(ReservationStatus.Released, reservation.Status);
        Assert.Equal(0, stock["A"].QuantityReserved);
    }

    [Fact]
    public void AdjustTo_CannotGoBelowReservedQuantity()
    {
        var item = StockItem.Create("A", 10, Now);
        item.Reserve(6, Now);

        Assert.Equal("Stock.BelowReserved", item.AdjustTo(5, Now).Error.Code);
        Assert.True(item.AdjustTo(6, Now).IsSuccess);
        Assert.Equal(0, item.QuantityAvailable);
    }
}
