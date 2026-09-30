using System.Globalization;
using MediatR;
using Sales.Contracts.IntegrationEvents;

namespace Notifications.Application.Features.OrderEmails;

public sealed class SendOrderConfirmedEmail(EmailDispatcher dispatcher) : INotificationHandler<OrderConfirmedIntegrationEvent>
{
    public Task Handle(OrderConfirmedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        var lines = string.Join(Environment.NewLine, notification.Lines.Select(l =>
            $"  {l.Quantity} × {l.ProductName} ({l.Sku}) @ {l.UnitPrice.ToString("N2", CultureInfo.InvariantCulture)}"));

        return dispatcher.SendAsync(
            notification.EventId,
            notification.CustomerEmail,
            $"Order {ShortId(notification.OrderId)} confirmed",
            $"""
            Hi {notification.CustomerName},

            Your order has been confirmed:

            {lines}

            Total: {notification.Total.ToString("N2", CultureInfo.InvariantCulture)}
            """,
            cancellationToken);
    }

    internal static string ShortId(Guid id) => id.ToString("N")[..8].ToUpperInvariant();
}

public sealed class SendOrderRejectedEmail(EmailDispatcher dispatcher) : INotificationHandler<OrderRejectedIntegrationEvent>
{
    public Task Handle(OrderRejectedIntegrationEvent notification, CancellationToken cancellationToken)
        => dispatcher.SendAsync(
            notification.EventId,
            notification.CustomerEmail,
            $"Order {SendOrderConfirmedEmail.ShortId(notification.OrderId)} could not be fulfilled",
            $"""
            Hi {notification.CustomerName},

            Unfortunately we could not fulfil your order: {notification.Reason}
            You have not been charged.
            """,
            cancellationToken);
}

public sealed class SendOrderCancelledEmail(EmailDispatcher dispatcher) : INotificationHandler<OrderCancelledIntegrationEvent>
{
    public Task Handle(OrderCancelledIntegrationEvent notification, CancellationToken cancellationToken)
        => dispatcher.SendAsync(
            notification.EventId,
            notification.CustomerEmail,
            $"Order {SendOrderConfirmedEmail.ShortId(notification.OrderId)} cancelled",
            $"""
            Hi {notification.CustomerName},

            Your order totalling {notification.Total.ToString("N2", CultureInfo.InvariantCulture)} has been cancelled.
            """,
            cancellationToken);
}
