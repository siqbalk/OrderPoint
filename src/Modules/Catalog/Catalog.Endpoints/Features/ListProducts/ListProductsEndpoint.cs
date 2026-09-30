using BuildingBlocks.Pagination;
using BuildingBlocks.Web;
using Catalog.Application.Features;
using Catalog.Application.Features.ListProducts;
using Catalog.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Catalog.Endpoints.Features.ListProducts;

public static class ListProductsEndpoint
{
    public static void MapListProducts(this IEndpointRouteBuilder app)
    {
        app.MapGet("", async (string? search, bool? includeInactive, int? page, int? pageSize, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new ListProductsQuery(search, includeInactive ?? false, page ?? 1, pageSize ?? Paging.DefaultPageSize),
                cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("ListProducts")
        .WithSummary("Search products by name or SKU")
        .Produces<PagedResult<ProductResponse>>()
        .RequireAuthorization(CatalogPermissions.ProductsRead);
    }
}
