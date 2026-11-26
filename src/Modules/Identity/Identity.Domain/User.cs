using BuildingBlocks.Results;
using BuildingBlocks.Security;

namespace Identity.Domain;

/// <summary>
/// A person who signs in to exactly one tenant. Email addresses are unique
/// across the whole platform so sign-in needs only email + password.
/// </summary>
public sealed class User
{
    private User() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? PasswordHash { get; private set; }
    public TenantRole Role { get; private set; }
    public UserStatus Status { get; private set; }
    public string? InvitationTokenHash { get; private set; }
    public DateTimeOffset? InvitationExpiresOnUtc { get; private set; }
    public DateTimeOffset CreatedOnUtc { get; private set; }
    public DateTimeOffset? LastLoginOnUtc { get; private set; }

    public bool CanSignIn => Status == UserStatus.Active && PasswordHash is not null;

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static User CreateOwner(Guid tenantId, string email, string displayName, string passwordHash, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Email = NormalizeEmail(email),
        DisplayName = displayName.Trim(),
        PasswordHash = passwordHash,
        Role = TenantRole.Owner,
        Status = UserStatus.Active,
        CreatedOnUtc = now,
    };

    public static User Invite(
        Guid tenantId,
        string email,
        string displayName,
        TenantRole role,
        string invitationTokenHash,
        DateTimeOffset expiresOnUtc,
        DateTimeOffset now)
    {
        if (role == TenantRole.Owner)
            throw new ArgumentException("A tenant has exactly one owner; invite users as Admin or Member.", nameof(role));

        return new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = NormalizeEmail(email),
            DisplayName = displayName.Trim(),
            Role = role,
            Status = UserStatus.Invited,
            InvitationTokenHash = invitationTokenHash,
            InvitationExpiresOnUtc = expiresOnUtc,
            CreatedOnUtc = now,
        };
    }

    public Result AcceptInvitation(string passwordHash, DateTimeOffset now)
    {
        if (Status != UserStatus.Invited)
            return Result.Failure(Error.Conflict("Identity.InvitationAlreadyAccepted", "This invitation has already been accepted."));

        if (InvitationExpiresOnUtc is { } expires && expires < now)
            return Result.Failure(Error.Validation("Identity.InvitationExpired", "This invitation has expired. Ask an administrator to invite you again."));

        PasswordHash = passwordHash;
        Status = UserStatus.Active;
        InvitationTokenHash = null;
        InvitationExpiresOnUtc = null;
        LastLoginOnUtc = now;
        return Result.Success();
    }

    /// <summary>
    /// Makes the user an Admin or a Member. The owner's role is fixed, and
    /// nobody else can become owner: a tenant has exactly one.
    /// </summary>
    public Result ChangeRole(TenantRole role)
    {
        if (Role == TenantRole.Owner)
            return Result.Failure(Error.Conflict("Identity.CannotChangeOwnerRole", "The organisation owner's role cannot be changed."));

        if (role == TenantRole.Owner)
            return Result.Failure(Error.Validation("Identity.InvalidRole", "A tenant has exactly one owner; choose Admin or Member."));

        Role = role;
        return Result.Success();
    }

    /// <summary>
    /// Stops the user signing in and frees their seat on the plan; a pending
    /// invitation can no longer be accepted. Disabling twice is a no-op.
    /// </summary>
    public Result Disable()
    {
        if (Role == TenantRole.Owner)
            return Result.Failure(Error.Conflict("Identity.CannotDisableOwner", "The organisation's owner cannot be disabled."));

        Status = UserStatus.Disabled;
        InvitationTokenHash = null;
        InvitationExpiresOnUtc = null;
        return Result.Success();
    }

    /// <summary>
    /// Restores a disabled user's access. Enabling an active user is a no-op.
    /// A user disabled before accepting their invitation has no password, so
    /// there is no account to restore.
    /// </summary>
    public Result Enable()
    {
        if (Status == UserStatus.Active)
            return Result.Success();

        if (Status != UserStatus.Disabled)
            return Result.Failure(Error.Conflict("Identity.UserNotDisabled", "Only a disabled user can be enabled."));

        if (PasswordHash is null)
            return Result.Failure(Error.Conflict(
                "Identity.InvitationNeverAccepted",
                "This user never accepted their invitation, so there is no account to enable."));

        Status = UserStatus.Active;
        return Result.Success();
    }

    /// <summary>
    /// Changes the name shown to colleagues. The access token carries the name,
    /// so a new token reflects it from the next sign-in.
    /// </summary>
    public Result Rename(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return Result.Failure(Error.Validation("Identity.DisplayNameRequired", "A display name is required."));

        DisplayName = displayName.Trim();
        return Result.Success();
    }

    public void RecordLogin(DateTimeOffset now) => LastLoginOnUtc = now;
}

public enum UserStatus
{
    Invited = 0,
    Active = 1,
    Disabled = 2,
}
