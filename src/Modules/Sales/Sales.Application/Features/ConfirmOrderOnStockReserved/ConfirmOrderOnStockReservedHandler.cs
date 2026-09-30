using Inventory.Contracts.IntegrationEvents;
using MediatR;
using Microsoft.Extensions.Logging;
using Sales.Application.Abstractions;
using Sales.Contracts.IntegrationEvents;

namespace Sales.Application.Features.ConfirmOrderOnStockReserved;

/// <summary>
/// Inventory has reserved the stock: confirm the order and tell the rest of the
/// system (Notifications emails the customer, Reporting books the revenue).
/// </summary>
public sealed class ConfirmOrderOnStockReservedHandler(
    ISalesDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<ConfirmOrderOnStockReservedHandler> logger)
    : INotificationHandler<StockReservedIntegrationEvent>
{
    public async Task Handle(StockReservedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        var order = await dbContext.GetOrderAsync(notification.OrderId, cancellationToken);
        if (order is null)
        {
            logger.LogWarning("Stock reserved for unknown order {OrderId}", notification.OrderId);
            return;
        }

        var wasAlreadyConfirmed = order.ConfirmedOnUtc is not null;
        var now = timeProvider.GetUtcNow();
        var confirmed = order.Confirm(now);
        if (confirmed.IsFailure)
        {
            // Typically cancelled before the reservation landed; Inventory releases
            // the stock when it handles OrderCancelled, so nothing to do here.
            logger.LogInformation("Order {OrderId} not confirmed: {Reason}", order.Id, confirmed.Error.Message);
            return;
        }

        if (wasAlreadyConfirmed)
        {
            return;
        }

        dbContext.Outbox.Enqueue(new OrderConfirmedIntegrationEvent(
            Guid.NewGuid(), now, notification.TenantId, order.Id, order.CustomerName, order.CustomerEmail, order.Total, order.ToLineItems()));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
