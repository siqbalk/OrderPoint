using System.Security.Cryptography;
using System.Text;
using Identity.Domain;

namespace Identity.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}

public interface ITokenIssuer
{
    AccessToken Issue(User user, IReadOnlyCollection<string> permissions);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresOnUtc);

/// <summary>
/// One-time invitation tokens: 256 random bits, URL-safe. Only the SHA-256
/// hash is persisted, so a database leak does not expose usable invitations.
/// </summary>
public static class InvitationTokens
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public static (string Token, string Hash) Generate()
    {
        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return (token, Hash(token));
    }

    public static string Hash(string token)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
