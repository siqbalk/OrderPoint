using System.ComponentModel.DataAnnotations;
using System.Text;

namespace BuildingBlocks.Security;

/// <summary>
/// The "Auth" configuration section. Bound once and shared by the token issuer
/// (Identity) and the token validator (Host), so both always agree on issuer,
/// audience, and key.
/// </summary>
public sealed class JwtSettings : IValidatableObject
{
    public const string SectionName = "Auth";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 key. At least 32 bytes; supply it from user-secrets or an environment variable, never appsettings.json.</summary>
    public string SigningKey { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenLifetimeMinutes { get; set; } = 60;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Encoding.UTF8.GetByteCount(SigningKey) < 32)
        {
            yield return new ValidationResult(
                "Auth:SigningKey must be at least 32 bytes. Set it with 'dotnet user-secrets set \"Auth:SigningKey\" <value> --project src/Host' or the Auth__SigningKey environment variable.",
                [nameof(SigningKey)]);
        }
    }
}
