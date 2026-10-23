using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Results;
using Catalog.Application.Abstractions;
using Identity.Contracts;
using MediatR;

namespace Catalog.Application.Features.GetProductQuota;

public sealed class GetProductQuotaHandler(
    ICatalogDbContext dbContext,
    ITenantContext tenantContext,
    ITenantPlanProvider planProvider)
    : IRequestHandler<GetProductQuotaQuery, Result<ProductQuotaResponse>>
{
    public async Task<Result<ProductQuotaResponse>> Handle(GetProductQuotaQuery request, CancellationToken cancellationToken)
    {
        // Same rule as CreateProduct: no plan found means no limit is enforced.
        var plan = await planProvider.GetPlanAsync(tenantContext.RequiredTenantId, cancellationToken);
        var used = await dbContext.CountProductsAsync(cancellationToken);
        var max = plan?.MaxProducts;

        return new ProductQuotaResponse(plan?.Plan, used, max, max is { } m ? Math.Max(0, m - used) : null);
    }
}
