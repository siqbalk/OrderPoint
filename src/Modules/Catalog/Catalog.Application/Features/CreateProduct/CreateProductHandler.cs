using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Results;
using Catalog.Application.Abstractions;
using Catalog.Contracts.IntegrationEvents;
using Catalog.Domain;
using Identity.Contracts;
using MediatR;

namespace Catalog.Application.Features.CreateProduct;

public sealed class CreateProductHandler(
    ICatalogDbContext dbContext,
    ITenantContext tenantContext,
    ITenantPlanProvider planProvider,
    TimeProvider timeProvider)
    : IRequestHandler<CreateProductCommand, Result<ProductResponse>>
{
    public async Task<Result<ProductResponse>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;

        var sku = Product.NormalizeSku(request.Sku);
        if (await dbContext.SkuExistsAsync(sku, cancellationToken))
        {
            return Result.Failure<ProductResponse>(
                Error.Conflict("Catalog.DuplicateSku", $"A product with SKU '{sku}' already exists."));
        }

        // Plan quota, read synchronously from Identity through its published contract.
        var plan = await planProvider.GetPlanAsync(tenantId, cancellationToken);
        if (plan?.MaxProducts is { } maxProducts && await dbContext.CountProductsAsync(cancellationToken) >= maxProducts)
        {
            return Result.Failure<ProductResponse>(Error.Forbidden(
                "Catalog.PlanLimitReached",
                $"The {plan.Plan} plan allows at most {maxProducts} products. Upgrade the plan to add more."));
        }

        var now = timeProvider.GetUtcNow();
        var product = Product.Create(sku, request.Name, request.Description, request.Price, now);

        dbContext.AddProduct(product);
        dbContext.Outbox.Enqueue(new ProductCreatedIntegrationEvent(Guid.NewGuid(), now, tenantId, product.Id, product.Sku, product.Name));
        await dbContext.SaveChangesAsync(cancellationToken);

        return ProductResponse.From(product);
    }
}
