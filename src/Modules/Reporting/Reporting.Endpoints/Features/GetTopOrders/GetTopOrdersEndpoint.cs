using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Reporting.Application.Features.GetTopOrders;
using Reporting.Contracts;

namespace Reporting.Endpoints.Features.GetTopOrders;

public static class GetTopOrdersEndpoint
{
    public static void MapGetTopOrders(this IEndpointRouteBuilder app)
    {
        app.MapGet("/sales/top-orders", async (DateOnly? from, DateOnly? to, int? limit, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new GetTopOrdersQuery(from, to, limit ?? GetTopOrdersValidator.DefaultLimit),
                cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("GetTopOrders")
        .WithSummary("Largest non-cancelled orders by value (UTC dates). Defaults to the last 30 days, top 10.")
        .Produces<TopOrdersResponse>()
        .RequireAuthorization(ReportingPermissions.SalesRead);
    }
}
