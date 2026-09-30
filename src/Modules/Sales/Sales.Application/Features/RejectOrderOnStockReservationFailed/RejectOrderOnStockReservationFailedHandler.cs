using Inventory.Contracts.IntegrationEvents;
using MediatR;
using Microsoft.Extensions.Logging;
using Sales.Application.Abstractions;
using Sales.Contracts.IntegrationEvents;
using Sales.Domain;

namespace Sales.Application.Features.RejectOrderOnStockReservationFailed;

/// <summary>
/// Inventory could not reserve stock (another order took it after the
/// synchronous pre-check passed): reject the order and let the customer know.
/// </summary>
public sealed class RejectOrderOnStockReservationFailedHandler(
    ISalesDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<RejectOrderOnStockReservationFailedHandler> logger)
    : INotificationHandler<StockReservationFailedIntegrationEvent>
{
    public async Task Handle(StockReservationFailedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        var order = await dbContext.GetOrderAsync(notification.OrderId, cancellationToken);
        if (order is null)
        {
            logger.LogWarning("Stock reservation failed for unknown order {OrderId}", notification.OrderId);
            return;
        }

        if (order.Status == OrderStatus.Rejected)
        {
            return; // redelivery
        }

        var now = timeProvider.GetUtcNow();
        var rejected = order.Reject(notification.Reason, now);
        if (rejected.IsFailure)
        {
            logger.LogInformation("Order {OrderId} not rejected: {Reason}", order.Id, rejected.Error.Message);
            return;
        }

        dbContext.Outbox.Enqueue(new OrderRejectedIntegrationEvent(
            Guid.NewGuid(), now, notification.TenantId, order.Id, order.CustomerName, order.CustomerEmail, notification.Reason));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
