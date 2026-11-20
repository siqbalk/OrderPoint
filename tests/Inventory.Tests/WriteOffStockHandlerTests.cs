using Inventory.Application.Abstractions;
using Inventory.Application.Features.WriteOffStock;
using Inventory.Domain;
using NSubstitute;

namespace Inventory.Tests;

public sealed class WriteOffStockHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly IInventoryDbContext _dbContext = Substitute.For<IInventoryDbContext>();

    private WriteOffStockHandler CreateHandler() => new(_dbContext, TimeProvider.System);

    private StockItem GivenStock(int onHand, int reserved = 0)
    {
        var item = StockItem.Create("WIDGET-1", onHand, Now);
        if (reserved > 0)
            item.Reserve(reserved, Now);
        _dbContext.FindBySkuAsync("WIDGET-1", Arg.Any<CancellationToken>()).Returns(item);
        return item;
    }

    [Fact]
    public async Task WritesOff_UnreservedUnits_LookingUpByNormalisedSku()
    {
        GivenStock(onHand: 10, reserved: 4);

        var result = await CreateHandler().Handle(new WriteOffStockCommand(" widget-1 ", 6), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value.QuantityOnHand);
        Assert.Equal(4, result.Value.QuantityReserved);
        Assert.Equal(0, result.Value.QuantityAvailable);
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_WritingOffReservedUnits()
    {
        var item = GivenStock(onHand: 10, reserved: 4);

        var result = await CreateHandler().Handle(new WriteOffStockCommand("WIDGET-1", 7), CancellationToken.None);

        Assert.Equal("Stock.BelowReserved", result.Error.Code);
        Assert.Equal(10, item.QuantityOnHand);
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_NotFound_ForUnknownSku()
    {
        var result = await CreateHandler().Handle(new WriteOffStockCommand("MISSING", 1), CancellationToken.None);

        Assert.Equal("Stock.NotFound", result.Error.Code);
    }

    [Fact]
    public void Domain_RejectsNonPositiveQuantity()
    {
        var item = StockItem.Create("WIDGET-1", 10, Now);

        Assert.Equal("Stock.InvalidQuantity", item.WriteOff(0, Now).Error.Code);
        Assert.Equal(10, item.QuantityOnHand);
    }

    [Fact]
    public void Validator_RequiresSkuAndPositiveQuantity()
    {
        var validator = new WriteOffStockValidator();

        Assert.True(validator.Validate(new WriteOffStockCommand("WIDGET-1", 1)).IsValid);
        Assert.False(validator.Validate(new WriteOffStockCommand("", 1)).IsValid);
        Assert.False(validator.Validate(new WriteOffStockCommand("WIDGET-1", 0)).IsValid);
    }
}
