namespace Identity.Domain;

/// <summary>
/// A customer organisation — the unit of isolation for every other module's
/// data. Identity is the only module that knows tenants as entities; the rest
/// only see a tenant id on the token and on integration events.
/// </summary>
public sealed class Tenant
{
    private Tenant() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public SubscriptionPlan Plan { get; private set; }
    public TenantStatus Status { get; private set; }
    public DateTimeOffset CreatedOnUtc { get; private set; }
    public DateTimeOffset? PlanChangedOnUtc { get; private set; }

    public PlanLimits Limits => PlanLimits.For(Plan);

    public bool IsActive => Status == TenantStatus.Active;

    public static Tenant Register(string name, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tenant name is required.", nameof(name));

        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Plan = SubscriptionPlan.Free,
            Status = TenantStatus.Active,
            CreatedOnUtc = now,
        };
    }

    public void ChangePlan(SubscriptionPlan plan, DateTimeOffset now)
    {
        if (plan == Plan)
            return;

        Plan = plan;
        PlanChangedOnUtc = now;
    }

    public void Suspend() => Status = TenantStatus.Suspended;

    public void Reactivate() => Status = TenantStatus.Active;
}

public enum TenantStatus
{
    Active = 0,
    Suspended = 1,
}

public enum SubscriptionPlan
{
    Free = 0,
    Pro = 1,
    Enterprise = 2,
}

/// <summary>Usage limits per plan. <c>null</c> means unlimited.</summary>
public sealed record PlanLimits(int? MaxUsers, int? MaxProducts)
{
    public static PlanLimits For(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.Free => new PlanLimits(MaxUsers: 3, MaxProducts: 25),
        SubscriptionPlan.Pro => new PlanLimits(MaxUsers: 25, MaxProducts: 5_000),
        SubscriptionPlan.Enterprise => new PlanLimits(MaxUsers: null, MaxProducts: null),
        _ => throw new ArgumentOutOfRangeException(nameof(plan), plan, null),
    };
}
