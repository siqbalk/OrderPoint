using BuildingBlocks.Results;
using Catalog.Application.Abstractions;
using MediatR;

namespace Catalog.Application.Features.UpdateProduct;

public sealed class UpdateProductHandler(ICatalogDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<UpdateProductCommand, Result<ProductResponse>>
{
    public async Task<Result<ProductResponse>> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await dbContext.FindByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<ProductResponse>(CatalogErrors.ProductNotFound(request.ProductId));
        }

        var now = timeProvider.GetUtcNow();
        product.Update(request.Name, request.Description, request.Price, now);
        if (request.IsActive != product.IsActive)
        {
            if (request.IsActive) product.Activate(now);
            else product.Deactivate(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ProductResponse.From(product);
    }
}
