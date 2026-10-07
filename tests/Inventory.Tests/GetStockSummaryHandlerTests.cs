using Inventory.Application.Abstractions;
using Inventory.Application.Features.GetStockSummary;
using NSubstitute;

namespace Inventory.Tests;

public sealed class GetStockSummaryHandlerTests
{
    private readonly IInventoryDbContext _dbContext = Substitute.For<IInventoryDbContext>();

    private GetStockSummaryHandler CreateHandler() => new(_dbContext);

    [Fact]
    public async Task Returns_Totals_WithAvailableAsOnHandMinusReserved()
    {
        _dbContext.GetStockTotalsAsync(Arg.Any<CancellationToken>()).Returns(new StockTotals(3, 120, 15, 1));

        var result = await CreateHandler().Handle(new GetStockSummaryQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new StockSummaryResponse(3, 120, 15, 105, 1), result.Value);
    }

    [Fact]
    public async Task Returns_AllZeros_WhenTenantHasNoStock()
    {
        _dbContext.GetStockTotalsAsync(Arg.Any<CancellationToken>()).Returns(new StockTotals(0, 0, 0, 0));

        var result = await CreateHandler().Handle(new GetStockSummaryQuery(), CancellationToken.None);

        Assert.Equal(new StockSummaryResponse(0, 0, 0, 0, 0), result.Value);
    }
}
