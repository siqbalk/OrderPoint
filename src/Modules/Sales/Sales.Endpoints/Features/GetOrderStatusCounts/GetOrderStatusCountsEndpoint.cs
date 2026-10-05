using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sales.Application.Features.GetOrderStatusCounts;
using Sales.Contracts;

namespace Sales.Endpoints.Features.GetOrderStatusCounts;

public static class GetOrderStatusCountsEndpoint
{
    public static void MapGetOrderStatusCounts(this IEndpointRouteBuilder app)
    {
        app.MapGet("/status-counts", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetOrderStatusCountsQuery(), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("GetOrderStatusCounts")
        .WithSummary("Number of orders in each status")
        .Produces<OrderStatusCountsResponse>()
        .RequireAuthorization(SalesPermissions.OrdersRead);
    }
}
