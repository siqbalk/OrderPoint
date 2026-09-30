using Inventory.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;
using Sales.Contracts.IntegrationEvents;

namespace Inventory.Application.Features.ReleaseStockOnOrderCancelled;

/// <summary>
/// Compensating action for a cancelled order: returns its reserved stock.
/// Idempotent — releasing an already-released reservation is a no-op, and an
/// order that never got a reservation (rejected, or cancelled first) has nothing to release.
/// </summary>
public sealed class ReleaseStockOnOrderCancelledHandler(
    IInventoryDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<ReleaseStockOnOrderCancelledHandler> logger)
    : INotificationHandler<OrderCancelledIntegrationEvent>
{
    public async Task Handle(OrderCancelledIntegrationEvent notification, CancellationToken cancellationToken)
    {
        var reservation = await dbContext.FindReservationByOrderAsync(notification.OrderId, cancellationToken);
        if (reservation is null)
        {
            logger.LogInformation("No stock reservation for cancelled order {OrderId}; nothing to release", notification.OrderId);
            return;
        }

        var stock = await dbContext.FindBySkusAsync(reservation.Lines.Select(l => l.Sku).ToList(), cancellationToken);
        reservation.Release(stock, timeProvider.GetUtcNow());

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Released stock reservation for cancelled order {OrderId}", notification.OrderId);
    }
}
