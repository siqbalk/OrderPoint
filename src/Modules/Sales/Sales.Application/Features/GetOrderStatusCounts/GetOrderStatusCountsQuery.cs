using BuildingBlocks.Results;
using MediatR;
using Sales.Application.Abstractions;
using Sales.Domain;

namespace Sales.Application.Features.GetOrderStatusCounts;

/// <summary>How many of the tenant's orders are in each status, e.g. for a dashboard.</summary>
public sealed record GetOrderStatusCountsQuery : IRequest<Result<OrderStatusCountsResponse>>;

/// <param name="ByStatus">Every status, in lifecycle order, including those with zero orders.</param>
public sealed record OrderStatusCountsResponse(int Total, IReadOnlyList<OrderStatusCount> ByStatus);

public sealed record OrderStatusCount(OrderStatus Status, int Count);

public sealed class GetOrderStatusCountsHandler(ISalesDbContext dbContext)
    : IRequestHandler<GetOrderStatusCountsQuery, Result<OrderStatusCountsResponse>>
{
    public async Task<Result<OrderStatusCountsResponse>> Handle(GetOrderStatusCountsQuery request, CancellationToken cancellationToken)
    {
        var counts = await dbContext.CountOrdersByStatusAsync(cancellationToken);

        var byStatus = Enum.GetValues<OrderStatus>()
            .Select(s => new OrderStatusCount(s, counts.GetValueOrDefault(s)))
            .ToList();

        return new OrderStatusCountsResponse(byStatus.Sum(c => c.Count), byStatus);
    }
}
