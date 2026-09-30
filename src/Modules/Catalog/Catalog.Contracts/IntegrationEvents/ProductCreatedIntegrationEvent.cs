using BuildingBlocks.Messaging;

namespace Catalog.Contracts.IntegrationEvents;

/// <summary>Published when a product is added to a tenant's catalog. Inventory opens a stock record for it.</summary>
public sealed record ProductCreatedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid TenantId,
    Guid ProductId,
    string Sku,
    string Name) : IIntegrationEvent;
