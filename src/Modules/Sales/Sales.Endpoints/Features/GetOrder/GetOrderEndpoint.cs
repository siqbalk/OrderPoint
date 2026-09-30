using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sales.Application.Features;
using Sales.Application.Features.GetOrder;
using Sales.Contracts;

namespace Sales.Endpoints.Features.GetOrder;

public static class GetOrderEndpoint
{
    public static void MapGetOrder(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{orderId:guid}", async (Guid orderId, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetOrderQuery(orderId), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("GetOrder")
        .Produces<OrderResponse>()
        .RequireAuthorization(SalesPermissions.OrdersRead);
    }
}
