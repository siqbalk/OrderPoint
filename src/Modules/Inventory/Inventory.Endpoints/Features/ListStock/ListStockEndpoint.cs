using BuildingBlocks.Pagination;
using BuildingBlocks.Web;
using Inventory.Application.Features;
using Inventory.Application.Features.ListStock;
using Inventory.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Inventory.Endpoints.Features.ListStock;

public static class ListStockEndpoint
{
    public static void MapListStock(this IEndpointRouteBuilder app)
    {
        app.MapGet("", async (int? maxAvailable, int? page, int? pageSize, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ListStockQuery(maxAvailable, page ?? 1, pageSize ?? Paging.DefaultPageSize), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("ListStock")
        .WithSummary("Stock levels, optionally only SKUs at or below a low-stock threshold")
        .Produces<PagedResult<StockItemResponse>>()
        .RequireAuthorization(InventoryPermissions.StockRead);
    }
}
