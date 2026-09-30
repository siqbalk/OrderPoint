namespace BuildingBlocks.Web;

/// <summary>
/// Names of rate-limiting policies Host defines, so module endpoints can opt in
/// (<c>.RequireRateLimiting(RateLimitPolicies.Authentication)</c>) without
/// knowing the limits themselves.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Strict per-IP limit for anonymous credential endpoints (login, sign-up, invitation acceptance).</summary>
    public const string Authentication = "authentication";
}
