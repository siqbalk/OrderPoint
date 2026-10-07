using BuildingBlocks.Web;
using Inventory.Application.Features.GetStockSummary;
using Inventory.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Inventory.Endpoints.Features.GetStockSummary;

public static class GetStockSummaryEndpoint
{
    public static void MapGetStockSummary(this IEndpointRouteBuilder app)
    {
        app.MapGet("", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetStockSummaryQuery(), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("GetStockSummary")
        .WithSummary("Stock totals across all SKUs, including how many are out of stock")
        .Produces<StockSummaryResponse>()
        .RequireAuthorization(InventoryPermissions.StockRead);
    }
}
