using BuildingBlocks.Web;
using Catalog.Application.Features;
using Catalog.Application.Features.GetProduct;
using Catalog.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Catalog.Endpoints.Features.GetProduct;

public static class GetProductEndpoint
{
    public static void MapGetProduct(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{productId:guid}", async (Guid productId, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetProductQuery(productId), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("GetProduct")
        .Produces<ProductResponse>()
        .RequireAuthorization(CatalogPermissions.ProductsRead);
    }
}
