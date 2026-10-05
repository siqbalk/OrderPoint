using NSubstitute;
using Sales.Application.Abstractions;
using Sales.Application.Features.GetOrderStatusCounts;
using Sales.Domain;

namespace Sales.Tests;

public sealed class GetOrderStatusCountsHandlerTests
{
    private readonly ISalesDbContext _dbContext = Substitute.For<ISalesDbContext>();

    private GetOrderStatusCountsHandler CreateHandler() => new(_dbContext);

    [Fact]
    public async Task Returns_EveryStatus_WithZeroForMissing_AndTotal()
    {
        _dbContext.CountOrdersByStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<OrderStatus, int> { [OrderStatus.Placed] = 2, [OrderStatus.Confirmed] = 5 });

        var result = await CreateHandler().Handle(new GetOrderStatusCountsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value.Total);
        Assert.Equal(
            [
                new OrderStatusCount(OrderStatus.Placed, 2),
                new OrderStatusCount(OrderStatus.Confirmed, 5),
                new OrderStatusCount(OrderStatus.Rejected, 0),
                new OrderStatusCount(OrderStatus.Cancelled, 0),
            ],
            result.Value.ByStatus);
    }

    [Fact]
    public async Task Returns_AllZeros_WhenTenantHasNoOrders()
    {
        _dbContext.CountOrdersByStatusAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<OrderStatus, int>());

        var result = await CreateHandler().Handle(new GetOrderStatusCountsQuery(), CancellationToken.None);

        Assert.Equal(0, result.Value.Total);
        Assert.All(result.Value.ByStatus, c => Assert.Equal(0, c.Count));
        Assert.Equal(Enum.GetValues<OrderStatus>().Length, result.Value.ByStatus.Count);
    }
}
