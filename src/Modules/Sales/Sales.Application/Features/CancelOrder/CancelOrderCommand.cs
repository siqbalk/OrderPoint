using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Results;
using MediatR;
using Sales.Application.Abstractions;
using Sales.Contracts.IntegrationEvents;

namespace Sales.Application.Features.CancelOrder;

public sealed record CancelOrderCommand(Guid OrderId) : IRequest<Result<OrderResponse>>;

/// <summary>
/// Cancels an order and publishes OrderCancelled so Inventory can release the
/// stock it reserved. This is the compensating half of the order saga.
/// </summary>
public sealed class CancelOrderHandler(
    ISalesDbContext dbContext,
    ITenantContext tenantContext,
    TimeProvider timeProvider)
    : IRequestHandler<CancelOrderCommand, Result<OrderResponse>>
{
    public async Task<Result<OrderResponse>> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await dbContext.GetOrderAsync(request.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<OrderResponse>(SalesMappings.OrderNotFound(request.OrderId));
        }

        var now = timeProvider.GetUtcNow();
        var cancelled = order.Cancel(now);
        if (cancelled.IsFailure)
        {
            return Result.Failure<OrderResponse>(cancelled.Error);
        }

        dbContext.Outbox.Enqueue(new OrderCancelledIntegrationEvent(
            Guid.NewGuid(), now, tenantContext.RequiredTenantId, order.Id, order.CustomerName, order.CustomerEmail, order.Total));

        await dbContext.SaveChangesAsync(cancellationToken);

        return OrderResponse.From(order);
    }
}
