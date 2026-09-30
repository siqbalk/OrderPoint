using BuildingBlocks.Messaging;

namespace Sales.Contracts.IntegrationEvents;

/// <summary>Stock has been reserved for every line; the order will be fulfilled. Counts as revenue.</summary>
public sealed record OrderConfirmedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid TenantId,
    Guid OrderId,
    string CustomerName,
    string CustomerEmail,
    decimal Total,
    IReadOnlyList<OrderLineItem> Lines) : IIntegrationEvent;

/// <summary>Inventory could not reserve stock, so the order was rejected. No stock is held for it.</summary>
public sealed record OrderRejectedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid TenantId,
    Guid OrderId,
    string CustomerName,
    string CustomerEmail,
    string Reason) : IIntegrationEvent;

/// <summary>
/// The order was cancelled. Inventory releases any reservation it holds for
/// the order; Reporting removes it from revenue if it had been confirmed.
/// </summary>
public sealed record OrderCancelledIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid TenantId,
    Guid OrderId,
    string CustomerName,
    string CustomerEmail,
    decimal Total) : IIntegrationEvent;
