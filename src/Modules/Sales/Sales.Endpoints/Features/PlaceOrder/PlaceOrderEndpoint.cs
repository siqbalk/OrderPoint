using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sales.Application.Features;
using Sales.Application.Features.PlaceOrder;
using Sales.Contracts;

namespace Sales.Endpoints.Features.PlaceOrder;

public sealed record PlaceOrderRequest(string CustomerName, string CustomerEmail, IReadOnlyList<PlaceOrderLine> Lines);

public static class PlaceOrderEndpoint
{
    public static void MapPlaceOrder(this IEndpointRouteBuilder app)
    {
        app.MapPost("", async (PlaceOrderRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new PlaceOrderCommand(request.CustomerName, request.CustomerEmail, request.Lines ?? []), cancellationToken);
            return result.ToHttpResult(order => Results.Created($"/api/sales/orders/{order.Id}", order));
        })
        .WithName("PlaceOrder")
        .WithSummary("Place an order; it is confirmed or rejected asynchronously once Inventory reserves stock")
        .Produces<OrderResponse>(StatusCodes.Status201Created)
        .RequireAuthorization(SalesPermissions.OrdersCreate);
    }
}
