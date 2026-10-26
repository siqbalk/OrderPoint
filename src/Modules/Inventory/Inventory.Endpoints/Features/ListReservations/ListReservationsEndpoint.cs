using BuildingBlocks.Pagination;
using BuildingBlocks.Web;
using Inventory.Application.Features;
using Inventory.Application.Features.ListReservations;
using Inventory.Contracts;
using Inventory.Domain;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Inventory.Endpoints.Features.ListReservations;

public static class ListReservationsEndpoint
{
    public static void MapListReservations(this IEndpointRouteBuilder app)
    {
        app.MapGet("", async (ReservationStatus? status, string? sku, int? page, int? pageSize, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new ListReservationsQuery(status, sku, page ?? 1, pageSize ?? Paging.DefaultPageSize),
                cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("ListReservations")
        .WithSummary("Stock reservations, newest first, optionally by status and SKU")
        .Produces<PagedResult<StockReservationResponse>>()
        .RequireAuthorization(InventoryPermissions.StockRead);
    }
}
