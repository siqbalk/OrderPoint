using System.Security.Claims;
using System.Text;
using BuildingBlocks.Security;
using Identity.Application.Abstractions;
using Identity.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure.Security;

/// <summary>
/// Issues the HMAC-signed access tokens that Host's JwtBearer handler validates.
/// Both sides read the same <see cref="JwtSettings"/>. Swapping to an external
/// identity provider (Entra ID, Auth0, Keycloak) replaces this class and Host's
/// validation settings — no other module is affected, because they only ever
/// see the <c>tenant_id</c> and <c>permission</c> claims.
/// </summary>
public sealed class JwtTokenIssuer(IOptions<JwtSettings> settings, TimeProvider timeProvider) : ITokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(User user, IReadOnlyCollection<string> permissions)
    {
        var jwt = settings.Value;
        var now = timeProvider.GetUtcNow();
        var expires = now.AddMinutes(jwt.AccessTokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(ClaimNames.Subject, user.Id.ToString()),
            new(ClaimNames.Email, user.Email),
            new(ClaimNames.Name, user.DisplayName),
            new(ClaimNames.TenantId, user.TenantId.ToString()),
            new(ClaimNames.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(permissions.Select(p => new Claim(ClaimNames.Permission, p)));

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expires);
    }
}
