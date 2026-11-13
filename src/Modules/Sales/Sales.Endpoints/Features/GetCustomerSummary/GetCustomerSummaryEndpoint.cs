using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sales.Application.Features.GetCustomerSummary;
using Sales.Contracts;

namespace Sales.Endpoints.Features.GetCustomerSummary;

public static class GetCustomerSummaryEndpoint
{
    public static void MapGetCustomerSummary(this IEndpointRouteBuilder app)
    {
        // Email in the query string rather than the path, where '@' and '+' would need escaping.
        app.MapGet("/summary", async (string email, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetCustomerSummaryQuery(email), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("GetCustomerSummary")
        .WithSummary("One customer's order counts by status, total spent, and first/last order dates")
        .Produces<CustomerSummaryResponse>()
        .RequireAuthorization(SalesPermissions.OrdersRead);
    }
}
