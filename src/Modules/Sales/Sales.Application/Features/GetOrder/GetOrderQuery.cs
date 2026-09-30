using BuildingBlocks.Results;
using MediatR;
using Sales.Application.Abstractions;

namespace Sales.Application.Features.GetOrder;

public sealed record GetOrderQuery(Guid OrderId) : IRequest<Result<OrderResponse>>;

public sealed class GetOrderHandler(ISalesDbContext dbContext) : IRequestHandler<GetOrderQuery, Result<OrderResponse>>
{
    public async Task<Result<OrderResponse>> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await dbContext.GetOrderAsync(request.OrderId, cancellationToken);

        return order is null
            ? Result.Failure<OrderResponse>(SalesMappings.OrderNotFound(request.OrderId))
            : OrderResponse.From(order);
    }
}
