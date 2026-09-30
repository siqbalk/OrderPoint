using Sales.Domain;

namespace Sales.Tests;

public sealed class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private static Order NewOrder() => Order.Place(
        "Ada Lovelace",
        "ada@example.com",
        [new OrderLineDraft("SKU-1", "Widget", 2, 10m), new OrderLineDraft("SKU-2", "Gadget", 1, 5.50m)],
        Now);

    [Fact]
    public void Place_ComputesTotalFromLines()
    {
        var order = NewOrder();

        Assert.Equal(OrderStatus.Placed, order.Status);
        Assert.Equal(25.50m, order.Total);
        Assert.Equal(2, order.Lines.Count);
    }

    [Fact]
    public void Confirm_IsIdempotent()
    {
        var order = NewOrder();

        Assert.True(order.Confirm(Now).IsSuccess);
        Assert.True(order.Confirm(Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(Now, order.ConfirmedOnUtc);
    }

    [Fact]
    public void Confirm_AfterCancellation_Fails()
    {
        var order = NewOrder();
        order.Cancel(Now);

        var result = order.Confirm(Now);

        Assert.True(result.IsFailure);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Reject_RecordsReason_AndRejectedOrdersCannotBeCancelled()
    {
        var order = NewOrder();

        Assert.True(order.Reject("Insufficient stock for: SKU-1.", Now).IsSuccess);
        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.Equal("Insufficient stock for: SKU-1.", order.RejectionReason);
        Assert.Equal("Orders.InvalidStatus", order.Cancel(Now).Error.Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cancel_WorksFromPlacedOrConfirmed_ButOnlyOnce(bool confirmFirst)
    {
        var order = NewOrder();
        if (confirmFirst)
        {
            order.Confirm(Now);
        }

        Assert.True(order.Cancel(Now).IsSuccess);
        Assert.True(order.Cancel(Now).IsFailure);
    }

    [Fact]
    public void Place_RejectsEmptyOrders()
        => Assert.Throws<ArgumentException>(() => Order.Place("Ada", "ada@example.com", [], Now));
}
