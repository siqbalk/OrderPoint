using Identity.Domain;
using Microsoft.AspNetCore.Identity;
using IPasswordHasher = Identity.Application.Abstractions.IPasswordHasher;

namespace Identity.Infrastructure.Security;

/// <summary>
/// ASP.NET Core Identity's hasher (PBKDF2-HMAC-SHA512, 100k iterations, per-hash
/// salt, versioned format), used on its own without the rest of ASP.NET Core Identity.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public bool Verify(string passwordHash, string password)
        => _hasher.VerifyHashedPassword(null!, passwordHash, password) != PasswordVerificationResult.Failed;
}
