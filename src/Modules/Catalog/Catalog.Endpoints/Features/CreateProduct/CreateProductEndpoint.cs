using BuildingBlocks.Web;
using Catalog.Application.Features;
using Catalog.Application.Features.CreateProduct;
using Catalog.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Catalog.Endpoints.Features.CreateProduct;

public sealed record CreateProductRequest(string Sku, string Name, string? Description, decimal Price);

public static class CreateProductEndpoint
{
    public static void MapCreateProduct(this IEndpointRouteBuilder app)
    {
        app.MapPost("", async (CreateProductRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new CreateProductCommand(request.Sku, request.Name, request.Description, request.Price), cancellationToken);
            return result.ToHttpResult(product => Results.Created($"/api/catalog/products/{product.Id}", product));
        })
        .WithName("CreateProduct")
        .WithSummary("Add a product (counts against the plan's product quota)")
        .Produces<ProductResponse>(StatusCodes.Status201Created)
        .RequireAuthorization(CatalogPermissions.ProductsManage);
    }
}
