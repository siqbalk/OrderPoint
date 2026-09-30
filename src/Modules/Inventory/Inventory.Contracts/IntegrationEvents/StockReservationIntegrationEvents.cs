using BuildingBlocks.Messaging;

namespace Inventory.Contracts.IntegrationEvents;

/// <summary>Every line of the order has been reserved. Sales confirms the order.</summary>
public sealed record StockReservedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid TenantId,
    Guid OrderId,
    Guid ReservationId) : IIntegrationEvent;

/// <summary>At least one line could not be reserved, so nothing was reserved. Sales rejects the order.</summary>
public sealed record StockReservationFailedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid TenantId,
    Guid OrderId,
    string Reason,
    IReadOnlyList<string> UnavailableSkus) : IIntegrationEvent;
