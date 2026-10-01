using BuildingBlocks.Web;
using Catalog.Application.Features;
using Catalog.Application.Features.GetProductBySku;
using Catalog.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Catalog.Endpoints.Features.GetProductBySku;

public static class GetProductBySkuEndpoint
{
    public static void MapGetProductBySku(this IEndpointRouteBuilder app)
    {
        app.MapGet("/by-sku/{sku}", async (string sku, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetProductBySkuQuery(sku), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("GetProductBySku")
        .WithSummary("Look up a product by SKU")
        .Produces<ProductResponse>()
        .RequireAuthorization(CatalogPermissions.ProductsRead);
    }
}
