using BuildingBlocks.Web;
using Inventory.Application.Features.GetReservationByOrder;
using Inventory.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Inventory.Endpoints.Features.GetReservationByOrder;

public static class GetReservationByOrderEndpoint
{
    public static void MapGetReservationByOrder(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{orderId:guid}", async (Guid orderId, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetReservationByOrderQuery(orderId), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("GetReservationByOrder")
        .WithSummary("Stock reserved for an order, and whether it has been released")
        .Produces<StockReservationResponse>()
        .RequireAuthorization(InventoryPermissions.StockRead);
    }
}
