using BuildingBlocks.Security;
using FluentValidation;
using Identity.Application.Abstractions;
using Identity.Domain;

namespace Identity.Application.Features;

/// <summary>Returned by every flow that signs a user in (register, login, accept invitation).</summary>
public sealed record AuthTokenResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresOnUtc,
    Guid TenantId,
    Guid UserId);

internal static class AuthTokens
{
    public static AuthTokenResponse IssueFor(User user, ITokenIssuer tokenIssuer, IPermissionCatalog permissionCatalog)
    {
        var token = tokenIssuer.Issue(user, permissionCatalog.ForRole(user.Role));
        return new AuthTokenResponse(token.Token, "Bearer", token.ExpiresOnUtc, user.TenantId, user.Id);
    }
}

internal static class PasswordRules
{
    /// <summary>Length-based policy per NIST SP 800-63B: at least 8 characters, no composition rules.</summary>
    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule)
        => rule.NotEmpty().MinimumLength(8).MaximumLength(128);
}
