namespace BuildingBlocks.Security;

/// <summary>
/// JWT claim names shared by the token issuer (Identity) and the token
/// consumers (Host's JwtBearer setup, tenant resolution, <see cref="ICurrentUser"/>).
/// Host disables inbound claim mapping, so these raw names are what appear on
/// the ClaimsPrincipal.
/// </summary>
public static class ClaimNames
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string TenantId = "tenant_id";
    public const string Role = "role";
    public const string Permission = "permission";
}
