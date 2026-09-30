namespace Notifications.Domain;

/// <summary>
/// A message sent to someone because something happened in another module.
/// (SourceEventId, Recipient) is unique, which is what makes sending
/// idempotent when the outbox redelivers the triggering event.
/// </summary>
public sealed class Notification
{
    private Notification() { }

    public Guid Id { get; private set; }
    public Guid SourceEventId { get; private set; }
    public string Channel { get; private set; } = string.Empty;
    public string Recipient { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public DateTimeOffset SentOnUtc { get; private set; }

    public static Notification Sent(Guid sourceEventId, string channel, string recipient, string subject, string body, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        SourceEventId = sourceEventId,
        Channel = channel,
        Recipient = recipient,
        Subject = subject,
        Body = body,
        SentOnUtc = now,
    };
}

public static class NotificationChannels
{
    public const string Email = "email";
}
