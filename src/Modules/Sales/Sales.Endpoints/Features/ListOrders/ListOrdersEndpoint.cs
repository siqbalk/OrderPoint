using BuildingBlocks.Pagination;
using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sales.Application.Features;
using Sales.Application.Features.ListOrders;
using Sales.Contracts;
using Sales.Domain;

namespace Sales.Endpoints.Features.ListOrders;

public static class ListOrdersEndpoint
{
    public static void MapListOrders(this IEndpointRouteBuilder app)
    {
        app.MapGet("", async (OrderStatus? status, int? page, int? pageSize, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ListOrdersQuery(status, page ?? 1, pageSize ?? Paging.DefaultPageSize), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("ListOrders")
        .Produces<PagedResult<OrderSummaryResponse>>()
        .RequireAuthorization(SalesPermissions.OrdersRead);
    }
}
