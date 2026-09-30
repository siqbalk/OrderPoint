using MediatR;

namespace BuildingBlocks.Messaging;

/// <summary>
/// Marker for events a module publishes across module boundaries via the outbox.
/// The publishing module owns the event's shape (in its *.Contracts project);
/// consuming modules reference that Contracts project to handle it — never the
/// publisher's Application/Domain/Infrastructure.
///
/// Extends INotification so a consuming module can subscribe with a plain
/// MediatR INotificationHandler&lt;TEvent&gt; — the OutboxProcessor publishes
/// the deserialized event directly, no wrapper type needed.
///
/// Every event carries the tenant it belongs to. The OutboxProcessor restores
/// that tenant into the handler's scope before publishing, so consumers run
/// with the same tenant isolation as an HTTP request would.
/// </summary>
public interface IIntegrationEvent : INotification
{
    Guid EventId { get; }
    DateTimeOffset OccurredOnUtc { get; }
    Guid TenantId { get; }
}
