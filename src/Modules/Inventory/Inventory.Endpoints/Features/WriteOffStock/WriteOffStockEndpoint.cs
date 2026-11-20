using BuildingBlocks.Web;
using Inventory.Application.Features;
using Inventory.Application.Features.WriteOffStock;
using Inventory.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Inventory.Endpoints.Features.WriteOffStock;

public sealed record WriteOffStockRequest(int Quantity);

public static class WriteOffStockEndpoint
{
    public static void MapWriteOffStock(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{sku}/write-off", async (string sku, WriteOffStockRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new WriteOffStockCommand(sku, request.Quantity), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("WriteOffStock")
        .WithSummary("Remove damaged, lost or expired units from on-hand; reserved units cannot be written off")
        .Produces<StockItemResponse>()
        .RequireAuthorization(InventoryPermissions.StockAdjust);
    }
}
