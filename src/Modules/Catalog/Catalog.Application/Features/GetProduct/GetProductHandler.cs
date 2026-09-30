using BuildingBlocks.Results;
using Catalog.Application.Abstractions;
using MediatR;

namespace Catalog.Application.Features.GetProduct;

public sealed class GetProductHandler(ICatalogDbContext dbContext) : IRequestHandler<GetProductQuery, Result<ProductResponse>>
{
    public async Task<Result<ProductResponse>> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        var product = await dbContext.FindByIdAsync(request.ProductId, cancellationToken);

        return product is null
            ? Result.Failure<ProductResponse>(CatalogErrors.ProductNotFound(request.ProductId))
            : ProductResponse.From(product);
    }
}
