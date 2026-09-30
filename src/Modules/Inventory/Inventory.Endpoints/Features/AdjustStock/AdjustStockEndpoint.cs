using BuildingBlocks.Web;
using Inventory.Application.Features;
using Inventory.Application.Features.AdjustStock;
using Inventory.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Inventory.Endpoints.Features.AdjustStock;

public sealed record AdjustStockRequest(int QuantityOnHand);

public static class AdjustStockEndpoint
{
    public static void MapAdjustStock(this IEndpointRouteBuilder app)
    {
        app.MapPut("/{sku}", async (string sku, AdjustStockRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new AdjustStockCommand(sku, request.QuantityOnHand), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("AdjustStock")
        .WithSummary("Stocktake correction: set on-hand to the counted quantity")
        .Produces<StockItemResponse>()
        .RequireAuthorization(InventoryPermissions.StockAdjust);
    }
}
