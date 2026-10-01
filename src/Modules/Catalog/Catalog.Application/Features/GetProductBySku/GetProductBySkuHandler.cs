using BuildingBlocks.Results;
using Catalog.Application.Abstractions;
using Catalog.Domain;
using MediatR;

namespace Catalog.Application.Features.GetProductBySku;

public sealed class GetProductBySkuHandler(ICatalogDbContext dbContext) : IRequestHandler<GetProductBySkuQuery, Result<ProductResponse>>
{
    public async Task<Result<ProductResponse>> Handle(GetProductBySkuQuery request, CancellationToken cancellationToken)
    {
        var sku = Product.NormalizeSku(request.Sku);
        var product = await dbContext.FindBySkuAsync(sku, cancellationToken);

        return product is null
            ? Result.Failure<ProductResponse>(CatalogErrors.ProductSkuNotFound(sku))
            : ProductResponse.From(product);
    }
}
