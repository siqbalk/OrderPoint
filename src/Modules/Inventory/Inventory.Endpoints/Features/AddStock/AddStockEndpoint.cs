using BuildingBlocks.Web;
using Inventory.Application.Features;
using Inventory.Application.Features.AddStock;
using Inventory.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Inventory.Endpoints.Features.AddStock;

public sealed record AddStockRequest(string Sku, int Quantity);

public static class AddStockEndpoint
{
    public static void MapAddStock(this IEndpointRouteBuilder app)
    {
        app.MapPost("", async (AddStockRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new AddStockCommand(request.Sku, request.Quantity), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("AddStock")
        .WithSummary("Receive goods: add to on-hand quantity (creates the stock record if needed)")
        .Produces<StockItemResponse>()
        .RequireAuthorization(InventoryPermissions.StockAdjust);
    }
}
