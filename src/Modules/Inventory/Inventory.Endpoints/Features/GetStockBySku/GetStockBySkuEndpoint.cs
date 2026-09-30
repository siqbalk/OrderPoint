using BuildingBlocks.Web;
using Inventory.Application.Features;
using Inventory.Application.Features.GetStockBySku;
using Inventory.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Inventory.Endpoints.Features.GetStockBySku;

public static class GetStockBySkuEndpoint
{
    public static void MapGetStockBySku(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{sku}", async (string sku, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetStockBySkuQuery(sku), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("GetStockBySku")
        .Produces<StockItemResponse>()
        .RequireAuthorization(InventoryPermissions.StockRead);
    }
}
