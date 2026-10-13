using BuildingBlocks.Pagination;
using NSubstitute;
using Sales.Application.Abstractions;
using Sales.Application.Features.ListOrders;
using Sales.Domain;

namespace Sales.Tests;

public sealed class ListOrdersHandlerTests
{
    private readonly ISalesDbContext _dbContext = Substitute.For<ISalesDbContext>();

    private ListOrdersHandler CreateHandler() => new(_dbContext);

    [Fact]
    public async Task PassesCustomerEmail_ToTheQuery()
    {
        var order = Order.Place("Ada Lovelace", "ada@example.com", [new OrderLineDraft("SKU-1", "Widget", 2, 10m)], DateTimeOffset.UtcNow);
        _dbContext.ListOrdersAsync(OrderStatus.Placed, "ada@example.com", 1, 25, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Order>([order], 1, 25, 1));

        var result = await CreateHandler().Handle(
            new ListOrdersQuery(OrderStatus.Placed, "ada@example.com", 1, 25), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var summary = Assert.Single(result.Value.Items);
        Assert.Equal(order.Id, summary.Id);
        Assert.Equal(2, summary.ItemCount);
    }

    [Fact]
    public void Validator_RejectsOverlongCustomerEmail()
    {
        var validator = new ListOrdersValidator();

        Assert.True(validator.Validate(new ListOrdersQuery(null, "ada@example.com")).IsValid);
        Assert.True(validator.Validate(new ListOrdersQuery(null)).IsValid);
        Assert.False(validator.Validate(new ListOrdersQuery(null, new string('a', 257))).IsValid);
    }
}
