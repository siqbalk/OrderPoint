using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Reporting.Application.Features.GetMonthlySales;
using Reporting.Contracts;

namespace Reporting.Endpoints.Features.GetMonthlySales;

public static class GetMonthlySalesEndpoint
{
    public static void MapGetMonthlySales(this IEndpointRouteBuilder app)
    {
        app.MapGet("/sales/monthly", async (int? year, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetMonthlySalesQuery(year), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("GetMonthlySales")
        .WithSummary("Revenue and order counts per month (UTC) for one year. Defaults to the current year.")
        .Produces<MonthlySalesResponse>()
        .RequireAuthorization(ReportingPermissions.SalesRead);
    }
}
