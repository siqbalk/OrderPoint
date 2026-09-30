using BuildingBlocks.Web;
using Catalog.Application.Features;
using Catalog.Application.Features.UpdateProduct;
using Catalog.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Catalog.Endpoints.Features.UpdateProduct;

public sealed record UpdateProductRequest(string Name, string? Description, decimal Price, bool IsActive = true);

public static class UpdateProductEndpoint
{
    public static void MapUpdateProduct(this IEndpointRouteBuilder app)
    {
        app.MapPut("/{productId:guid}", async (Guid productId, UpdateProductRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new UpdateProductCommand(productId, request.Name, request.Description, request.Price, request.IsActive),
                cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("UpdateProduct")
        .WithSummary("Change name, description, price, or active status")
        .Produces<ProductResponse>()
        .RequireAuthorization(CatalogPermissions.ProductsManage);
    }
}
