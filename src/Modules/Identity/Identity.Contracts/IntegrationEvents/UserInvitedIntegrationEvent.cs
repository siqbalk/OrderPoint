using BuildingBlocks.Messaging;

namespace Identity.Contracts.IntegrationEvents;

/// <summary>
/// Published when an administrator invites someone to their tenant. Carries the
/// one-time invitation token so Notifications can email the accept link; only
/// its hash is stored in Identity's own tables.
/// </summary>
public sealed record UserInvitedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid TenantId,
    string TenantName,
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    string InvitationToken,
    DateTimeOffset ExpiresOnUtc) : IIntegrationEvent;
