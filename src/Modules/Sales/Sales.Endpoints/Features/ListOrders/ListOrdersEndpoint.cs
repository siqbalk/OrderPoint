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
        app.MapGet("", async (
            OrderStatus? status,
            string? customerEmail,
            DateOnly? from,
            DateOnly? to,
            int? page,
            int? pageSize,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new ListOrdersQuery(status, customerEmail, from, to, page ?? 1, pageSize ?? Paging.DefaultPageSize),
                cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("ListOrders")
        .WithSummary("Orders, newest first, optionally filtered by status, customer email and placed date range (UTC, inclusive)")
        .Produces<PagedResult<OrderSummaryResponse>>()
        .RequireAuthorization(SalesPermissions.OrdersRead);
    }
}
