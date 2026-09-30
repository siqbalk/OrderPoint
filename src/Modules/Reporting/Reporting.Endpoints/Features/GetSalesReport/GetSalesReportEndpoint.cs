using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Reporting.Application.Features.GetSalesReport;
using Reporting.Contracts;

namespace Reporting.Endpoints.Features.GetSalesReport;

public static class GetSalesReportEndpoint
{
    public static void MapGetSalesReport(this IEndpointRouteBuilder app)
    {
        app.MapGet("/sales", async (DateOnly? from, DateOnly? to, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetSalesReportQuery(from, to), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("GetSalesReport")
        .WithSummary("Revenue and order counts per day (UTC). Defaults to the last 30 days.")
        .Produces<SalesReportResponse>()
        .RequireAuthorization(ReportingPermissions.SalesRead);
    }
}
