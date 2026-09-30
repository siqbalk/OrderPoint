using BuildingBlocks.Messaging;

namespace Sales.Contracts.IntegrationEvents;

/// <summary>
/// Published by Sales via the outbox when an order is placed. Owned by Sales —
/// any module that needs to react (Inventory reserves stock) references
/// Sales.Contracts to know its shape, never Sales.Application/Domain/Infrastructure.
/// </summary>
public sealed record OrderPlacedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid TenantId,
    Guid OrderId,
    string CustomerName,
    string CustomerEmail,
    decimal Total,
    IReadOnlyList<OrderLineItem> Lines) : IIntegrationEvent;

/// <summary>One line of an order as seen by other modules.</summary>
public sealed record OrderLineItem(string Sku, string ProductName, int Quantity, decimal UnitPrice);
