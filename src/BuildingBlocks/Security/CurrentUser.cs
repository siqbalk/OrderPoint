using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Security;

/// <summary>The authenticated caller, for handlers that need to know who is acting (auditing, self-service).</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    string? Email { get; }
    IReadOnlyCollection<string> Permissions { get; }

    bool HasPermission(string permission) => Permissions.Contains(permission);
}

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private System.Security.Claims.ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => Guid.TryParse(Principal?.FindFirst(ClaimNames.Subject)?.Value, out var id) ? id : null;

    public string? Email => Principal?.FindFirst(ClaimNames.Email)?.Value;

    public IReadOnlyCollection<string> Permissions =>
        Principal?.FindAll(ClaimNames.Permission).Select(c => c.Value).ToArray() ?? [];
}
