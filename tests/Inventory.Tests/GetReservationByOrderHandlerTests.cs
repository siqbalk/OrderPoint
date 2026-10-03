using Inventory.Application.Abstractions;
using Inventory.Application.Features.GetReservationByOrder;
using Inventory.Domain;
using NSubstitute;

namespace Inventory.Tests;

public sealed class GetReservationByOrderHandlerTests
{
    private readonly IInventoryDbContext _dbContext = Substitute.For<IInventoryDbContext>();

    private GetReservationByOrderHandler CreateHandler() => new(_dbContext);

    [Fact]
    public async Task Returns_Reservation_WithLines()
    {
        var orderId = Guid.NewGuid();
        var stock = new Dictionary<string, StockItem> { ["WIDGET-1"] = StockItem.Create("WIDGET-1", 10, DateTimeOffset.UtcNow) };
        var reservation = StockReservation.TryReserve(orderId, [new ReservationLine("WIDGET-1", 3)], stock, DateTimeOffset.UtcNow).Value;
        _dbContext.FindReservationByOrderAsync(orderId, Arg.Any<CancellationToken>()).Returns(reservation);

        var result = await CreateHandler().Handle(new GetReservationByOrderQuery(orderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(orderId, result.Value.OrderId);
        Assert.Equal(ReservationStatus.Active, result.Value.Status);
        var line = Assert.Single(result.Value.Lines);
        Assert.Equal(new ReservationLineResponse("WIDGET-1", 3), line);
    }

    [Fact]
    public async Task Returns_NotFound_WhenOrderHasNoReservation()
    {
        var result = await CreateHandler().Handle(new GetReservationByOrderQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal("Stock.ReservationNotFound", result.Error.Code);
    }

    [Fact]
    public void Validator_RejectsEmptyOrderId()
    {
        var validator = new GetReservationByOrderValidator();

        Assert.False(validator.Validate(new GetReservationByOrderQuery(Guid.Empty)).IsValid);
        Assert.True(validator.Validate(new GetReservationByOrderQuery(Guid.NewGuid())).IsValid);
    }
}
