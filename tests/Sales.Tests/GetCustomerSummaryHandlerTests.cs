using NSubstitute;
using Sales.Application.Abstractions;
using Sales.Application.Features;
using Sales.Application.Features.GetCustomerSummary;
using Sales.Domain;

namespace Sales.Tests;

public sealed class GetCustomerSummaryHandlerTests
{
    private static readonly DateTimeOffset Jan = new(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Mar = new(2026, 3, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset May = new(2026, 5, 20, 9, 0, 0, TimeSpan.Zero);

    private readonly ISalesDbContext _dbContext = Substitute.For<ISalesDbContext>();

    private GetCustomerSummaryHandler CreateHandler() => new(_dbContext);

    [Fact]
    public async Task Summarises_NormalisingTheEmail_AndCountingOnlyConfirmedSpend()
    {
        _dbContext.GetCustomerOrderTotalsAsync("ada@example.com", Arg.Any<CancellationToken>()).Returns(
        [
            new CustomerOrderStatusTotals(OrderStatus.Confirmed, 2, 150m, Mar, May),
            new CustomerOrderStatusTotals(OrderStatus.Cancelled, 1, 40m, Jan, Jan),
        ]);

        var result = await CreateHandler().Handle(new GetCustomerSummaryQuery(" Ada@Example.COM "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ada@example.com", result.Value.CustomerEmail);
        Assert.Equal(3, result.Value.TotalOrders);
        Assert.Equal(150m, result.Value.TotalSpent);
        Assert.Equal(Jan, result.Value.FirstOrderOnUtc);
        Assert.Equal(May, result.Value.LastOrderOnUtc);
        Assert.Equal(
            [
                new OrderStatusCount(OrderStatus.Placed, 0),
                new OrderStatusCount(OrderStatus.Confirmed, 2),
                new OrderStatusCount(OrderStatus.Rejected, 0),
                new OrderStatusCount(OrderStatus.Cancelled, 1),
            ],
            result.Value.ByStatus);
    }

    [Fact]
    public async Task Returns_NotFound_WhenTheCustomerHasNoOrders()
    {
        _dbContext.GetCustomerOrderTotalsAsync("nobody@example.com", Arg.Any<CancellationToken>()).Returns([]);

        var result = await CreateHandler().Handle(new GetCustomerSummaryQuery("nobody@example.com"), CancellationToken.None);

        Assert.Equal("Orders.CustomerNotFound", result.Error.Code);
    }

    [Fact]
    public void Validator_RequiresEmail()
    {
        var validator = new GetCustomerSummaryValidator();

        Assert.True(validator.Validate(new GetCustomerSummaryQuery("ada@example.com")).IsValid);
        Assert.False(validator.Validate(new GetCustomerSummaryQuery(" ")).IsValid);
        Assert.False(validator.Validate(new GetCustomerSummaryQuery(new string('a', 257))).IsValid);
    }
}
