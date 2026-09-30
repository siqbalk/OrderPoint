namespace BuildingBlocks.MultiTenancy;

/// <summary>
/// The tenant the current scope (HTTP request or outbox dispatch) runs as.
/// Every module's DbContext filters its tenant-scoped tables by this value and
/// stamps it on new rows, so application code never filters by tenant by hand.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }

    bool HasTenant => TenantId is not null;

    /// <summary>The tenant id, or an exception when the scope has none (e.g. an anonymous request).</summary>
    Guid RequiredTenantId => TenantId ?? throw new InvalidOperationException("No tenant is associated with the current scope.");
}

/// <summary>
/// Write side of <see cref="ITenantContext"/>, used only by the infrastructure
/// that establishes a scope's tenant: the tenant-resolution middleware, the
/// outbox processor, and Identity's tenant registration.
/// </summary>
public interface ITenantSetter
{
    void SetTenant(Guid tenantId);
}

public sealed class TenantContext : ITenantContext, ITenantSetter
{
    public Guid? TenantId { get; private set; }

    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId));
        }

        if (TenantId is { } current && current != tenantId)
        {
            throw new InvalidOperationException("The tenant of a scope cannot be changed once set.");
        }

        TenantId = tenantId;
    }
}
