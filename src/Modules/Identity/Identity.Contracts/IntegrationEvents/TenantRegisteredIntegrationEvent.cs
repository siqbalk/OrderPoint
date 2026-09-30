using BuildingBlocks.Messaging;

namespace Identity.Contracts.IntegrationEvents;

/// <summary>Published when a new organisation signs up.</summary>
public sealed record TenantRegisteredIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid TenantId,
    string TenantName,
    Guid OwnerUserId,
    string OwnerEmail,
    string OwnerDisplayName) : IIntegrationEvent;
