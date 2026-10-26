using BuildingBlocks.Pagination;
using Inventory.Application.Abstractions;
using Inventory.Application.Features.ListReservations;
using Inventory.Domain;
using NSubstitute;

namespace Inventory.Tests;

public sealed class ListReservationsHandlerTests
{
    private readonly IInventoryDbContext _dbContext = Substitute.For<IInventoryDbContext>();

    private ListReservationsHandler CreateHandler() => new(_dbContext);

    [Fact]
    public async Task NormalisesSku_AndReturnsReservationsWithLines()
    {
        var orderId = Guid.NewGuid();
        var stock = new Dictionary<string, StockItem> { ["WIDGET-1"] = StockItem.Create("WIDGET-1", 10, DateTimeOffset.UtcNow) };
        var reservation = StockReservation.TryReserve(orderId, [new ReservationLine("WIDGET-1", 3)], stock, DateTimeOffset.UtcNow).Value;
        _dbContext.ListReservationsAsync(ReservationStatus.Active, "WIDGET-1", 1, 25, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<StockReservation>([reservation], 1, 25, 1));

        var result = await CreateHandler().Handle(
            new ListReservationsQuery(ReservationStatus.Active, " widget-1 ", 1, 25), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(orderId, item.OrderId);
        Assert.Equal(3, Assert.Single(item.Lines).Quantity);
    }

    [Fact]
    public async Task BlankSku_IsNotApplied()
    {
        _dbContext.ListReservationsAsync(null, null, 1, 25, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<StockReservation>([], 1, 25, 0));

        var result = await CreateHandler().Handle(new ListReservationsQuery(null, " ", 1, 25), CancellationToken.None);

        Assert.True(result.IsSuccess);
        await _dbContext.Received(1).ListReservationsAsync(null, null, 1, 25, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_RejectsUndefinedStatus_AndOverlongSku()
    {
        var validator = new ListReservationsValidator();

        Assert.True(validator.Validate(new ListReservationsQuery(ReservationStatus.Released, "WIDGET-1")).IsValid);
        Assert.False(validator.Validate(new ListReservationsQuery((ReservationStatus)42)).IsValid);
        Assert.False(validator.Validate(new ListReservationsQuery(null, new string('A', 51))).IsValid);
    }
}
