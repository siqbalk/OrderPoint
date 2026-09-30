namespace Identity.Contracts;

/// <summary>
/// Synchronous cross-module contract, owned and implemented by Identity:
/// lets other modules enforce subscription-plan limits (e.g. Catalog's product
/// quota) without knowing how plans are stored.
/// </summary>
public interface ITenantPlanProvider
{
    Task<TenantPlanInfo?> GetPlanAsync(Guid tenantId, CancellationToken cancellationToken);
}

/// <param name="MaxUsers"><c>null</c> means unlimited.</param>
/// <param name="MaxProducts"><c>null</c> means unlimited.</param>
public sealed record TenantPlanInfo(Guid TenantId, string Plan, bool IsActive, int? MaxUsers, int? MaxProducts);
