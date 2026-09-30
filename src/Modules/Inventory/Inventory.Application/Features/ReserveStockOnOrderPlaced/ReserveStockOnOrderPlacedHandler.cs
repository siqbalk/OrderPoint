using Inventory.Application.Abstractions;
using Inventory.Contracts.IntegrationEvents;
using Inventory.Domain;
using MediatR;
using Microsoft.Extensions.Logging;
using Sales.Contracts.IntegrationEvents;

namespace Inventory.Application.Features.ReserveStockOnOrderPlaced;

/// <summary>
/// Reacts to Sales' OrderPlacedIntegrationEvent (published via Sales' outbox and
/// republished in-process by the OutboxProcessor). This is the cross-module async
/// flow: Inventory depends on Sales.Contracts only — never Sales.Application,
/// Sales.Domain, or Sales.Infrastructure.
///
/// The outcome goes back to Sales as another integration event through
/// Inventory's own outbox, written in the same transaction as the reservation —
/// a choreographed saga with no shared transaction between the two modules.
/// </summary>
public sealed class ReserveStockOnOrderPlacedHandler(
    IInventoryDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<ReserveStockOnOrderPlacedHandler> logger)
    : INotificationHandler<OrderPlacedIntegrationEvent>
{
    public async Task Handle(OrderPlacedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        // Idempotency: the outbox delivers at least once.
        if (await dbContext.FindReservationByOrderAsync(notification.OrderId, cancellationToken) is not null)
        {
            logger.LogInformation("Stock for order {OrderId} is already reserved; ignoring redelivery", notification.OrderId);
            return;
        }

        var now = timeProvider.GetUtcNow();
        var requested = notification.Lines.Select(l => new ReservationLine(l.Sku, l.Quantity)).ToList();
        var stock = await dbContext.FindBySkusAsync(
            requested.Select(l => StockItem.NormalizeSku(l.Sku)).Distinct().ToList(),
            cancellationToken);

        var reservation = StockReservation.TryReserve(notification.OrderId, requested, stock, now);

        if (reservation.IsSuccess)
        {
            dbContext.AddReservation(reservation.Value);
            dbContext.Outbox.Enqueue(new StockReservedIntegrationEvent(
                Guid.NewGuid(), now, notification.TenantId, notification.OrderId, reservation.Value.Id));

            logger.LogInformation("Reserved stock for order {OrderId}", notification.OrderId);
        }
        else
        {
            dbContext.Outbox.Enqueue(new StockReservationFailedIntegrationEvent(
                Guid.NewGuid(), now, notification.TenantId, notification.OrderId, reservation.Error.Message,
                StockReservation.FindShortages(requested, stock)));

            logger.LogInformation("Could not reserve stock for order {OrderId}: {Reason}", notification.OrderId, reservation.Error.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
