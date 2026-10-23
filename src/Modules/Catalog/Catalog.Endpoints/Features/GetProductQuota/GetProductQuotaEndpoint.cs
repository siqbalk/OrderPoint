using BuildingBlocks.Web;
using Catalog.Application.Features.GetProductQuota;
using Catalog.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Catalog.Endpoints.Features.GetProductQuota;

public static class GetProductQuotaEndpoint
{
    public static void MapGetProductQuota(this IEndpointRouteBuilder app)
    {
        app.MapGet("/quota", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetProductQuotaQuery(), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("GetProductQuota")
        .WithSummary("Products used against the tenant's plan limit")
        .Produces<ProductQuotaResponse>()
        .RequireAuthorization(CatalogPermissions.ProductsRead);
    }
}
