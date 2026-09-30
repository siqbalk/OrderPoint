using MediatR;
using Reporting.Application.Abstractions;
using Reporting.Domain;
using Sales.Contracts.IntegrationEvents;

namespace Reporting.Application.Features.SalesProjection;

/// <summary>Books a confirmed order into the sales read model. Idempotent by OrderId.</summary>
public sealed class RecordSaleOnOrderConfirmed(IReportingDbContext dbContext) : INotificationHandler<OrderConfirmedIntegrationEvent>
{
    public async Task Handle(OrderConfirmedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        if (await dbContext.FindSalesRecordAsync(notification.OrderId, cancellationToken) is not null)
        {
            return;
        }

        dbContext.AddSalesRecord(SalesRecord.Confirmed(
            notification.OrderId,
            notification.OccurredOnUtc,
            notification.Total,
            notification.Lines.Sum(l => l.Quantity)));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Removes a cancelled order from revenue. Orders cancelled before confirmation were never booked.</summary>
public sealed class ReverseSaleOnOrderCancelled(IReportingDbContext dbContext) : INotificationHandler<OrderCancelledIntegrationEvent>
{
    public async Task Handle(OrderCancelledIntegrationEvent notification, CancellationToken cancellationToken)
    {
        var record = await dbContext.FindSalesRecordAsync(notification.OrderId, cancellationToken);
        if (record is null)
        {
            return;
        }

        record.Cancel(notification.OccurredOnUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
