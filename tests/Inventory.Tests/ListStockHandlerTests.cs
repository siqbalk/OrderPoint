using BuildingBlocks.Pagination;
using Inventory.Application.Abstractions;
using Inventory.Application.Features.ListStock;
using Inventory.Domain;
using NSubstitute;

namespace Inventory.Tests;

public sealed class ListStockHandlerTests
{
    private readonly IInventoryDbContext _dbContext = Substitute.For<IInventoryDbContext>();

    private ListStockHandler CreateHandler() => new(_dbContext);

    [Fact]
    public async Task NormalisesSearch_BeforeQuerying()
    {
        var item = StockItem.Create("WIDGET-1", 4, DateTimeOffset.UtcNow);
        _dbContext.ListAsync("WIDG", 5, 1, 25, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<StockItem>([item], 1, 25, 1));

        var result = await CreateHandler().Handle(new ListStockQuery(5, " widg ", 1, 25), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("WIDGET-1", Assert.Single(result.Value.Items).Sku);
    }

    [Fact]
    public async Task BlankSearch_IsNotApplied()
    {
        _dbContext.ListAsync(null, null, 1, 25, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<StockItem>([], 1, 25, 0));

        var result = await CreateHandler().Handle(new ListStockQuery(null, "  ", 1, 25), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await _dbContext.Received(1).ListAsync(null, null, 1, 25, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_RejectsOverlongSearch()
    {
        var validator = new ListStockValidator();

        Assert.True(validator.Validate(new ListStockQuery(null, new string('A', 50))).IsValid);
        Assert.False(validator.Validate(new ListStockQuery(null, new string('A', 51))).IsValid);
    }
}
