using BuildingBlocks.Results;
using Notifications.Domain;

namespace Notifications.Application.Features;

public sealed record NotificationResponse(Guid Id, Guid SourceEventId, string Channel, string Recipient, string Subject, string Body, DateTimeOffset SentOnUtc)
{
    public static NotificationResponse From(Notification n)
        => new(n.Id, n.SourceEventId, n.Channel, n.Recipient, n.Subject, n.Body, n.SentOnUtc);
}

internal static class NotificationsErrors
{
    public static Error NotificationNotFound(Guid notificationId)
        => Error.NotFound("Notification.NotFound", $"Notification '{notificationId}' was not found.");
}
