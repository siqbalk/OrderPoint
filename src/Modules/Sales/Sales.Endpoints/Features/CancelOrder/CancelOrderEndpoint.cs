using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sales.Application.Features;
using Sales.Application.Features.CancelOrder;
using Sales.Contracts;

namespace Sales.Endpoints.Features.CancelOrder;

public static class CancelOrderEndpoint
{
    public static void MapCancelOrder(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{orderId:guid}/cancel", async (Guid orderId, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new CancelOrderCommand(orderId), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("CancelOrder")
        .WithSummary("Cancel an order; any reserved stock is released asynchronously")
        .Produces<OrderResponse>()
        .RequireAuthorization(SalesPermissions.OrdersCancel);
    }
}
