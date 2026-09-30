using Identity.Contracts;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.InternalServices;

/// <summary>Implementation of Identity's published <see cref="ITenantPlanProvider"/> contract.</summary>
public sealed class TenantPlanProvider(IdentityDbContext dbContext) : ITenantPlanProvider
{
    public async Task<TenantPlanInfo?> GetPlanAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        var limits = tenant.Limits;
        return new TenantPlanInfo(tenant.Id, tenant.Plan.ToString(), tenant.IsActive, limits.MaxUsers, limits.MaxProducts);
    }
}
