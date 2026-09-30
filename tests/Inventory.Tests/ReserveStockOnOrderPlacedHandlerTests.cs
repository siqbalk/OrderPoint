using BuildingBlocks.Outbox;
using Inventory.Application.Abstractions;
using Inventory.Application.Features.ReserveStockOnOrderPlaced;
using Inventory.Contracts.IntegrationEvents;
using Inventory.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sales.Contracts.IntegrationEvents;

namespace Inventory.Tests;

public sealed class ReserveStockOnOrderPlacedHandlerTests
{
    private readonly IInventoryDbContext _dbContext = Substitute.For<IInventoryDbContext>();
    private readonly IOutboxWriter _outbox = Substitute.For<IOutboxWriter>();

    public ReserveStockOnOrderPlacedHandlerTests() => _dbContext.Outbox.Returns(_outbox);

    private ReserveStockOnOrderPlacedHandler CreateHandler()
        => new(_dbContext, TimeProvider.System, NullLogger<ReserveStockOnOrderPlacedHandler>.Instance);

    private static OrderPlacedIntegrationEvent OrderPlaced(int quantity) => new(
        Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid(), "Ada", "ada@example.com", 10m,
        [new OrderLineItem("SKU-1", "Widget", quantity, 1m)]);

    private void GivenStock(int onHand)
        => _dbContext.FindBySkusAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, StockItem> { ["SKU-1"] = StockItem.Create("SKU-1", onHand, DateTimeOffset.UtcNow) });

    [Fact]
    public async Task Reserves_AndPublishesStockReserved()
    {
        GivenStock(10);
        var @event = OrderPlaced(4);

        await CreateHandler().Handle(@event, CancellationToken.None);

        _dbContext.Received(1).AddReservation(Arg.Is<StockReservation>(r => r.OrderId == @event.OrderId));
        _outbox.Received(1).Enqueue(Arg.Is<StockReservedIntegrationEvent>(e => e.OrderId == @event.OrderId && e.TenantId == @event.TenantId));
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishesReservationFailed_WhenStockIsShort()
    {
        GivenStock(2);
        var @event = OrderPlaced(4);

        await CreateHandler().Handle(@event, CancellationToken.None);

        _dbContext.DidNotReceive().AddReservation(Arg.Any<StockReservation>());
        _outbox.Received(1).Enqueue(Arg.Is<StockReservationFailedIntegrationEvent>(e => e.UnavailableSkus.Single() == "SKU-1"));
    }

    [Fact]
    public async Task IgnoresRedelivery_WhenOrderAlreadyReserved()
    {
        var @event = OrderPlaced(1);
        var stock = new Dictionary<string, StockItem> { ["SKU-1"] = StockItem.Create("SKU-1", 5, DateTimeOffset.UtcNow) };
        var existing = StockReservation.TryReserve(@event.OrderId, [new ReservationLine("SKU-1", 1)], stock, DateTimeOffset.UtcNow).Value;
        _dbContext.FindReservationByOrderAsync(@event.OrderId, Arg.Any<CancellationToken>()).Returns(existing);

        await CreateHandler().Handle(@event, CancellationToken.None);

        _outbox.DidNotReceiveWithAnyArgs().Enqueue(default!);
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
