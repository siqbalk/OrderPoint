using Microsoft.Extensions.Logging;
using Notifications.Application.Abstractions;
using Notifications.Domain;

namespace Notifications.Application.Features;

/// <summary>
/// Sends an email once per (triggering event, recipient) and records it.
/// If sending throws, nothing is recorded and the exception propagates, so the
/// outbox retries the event later; a successful send is recorded and never repeated.
/// </summary>
public sealed class EmailDispatcher(
    INotificationsDbContext dbContext,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    ILogger<EmailDispatcher> logger)
{
    public async Task SendAsync(Guid sourceEventId, string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        if (await dbContext.ExistsAsync(sourceEventId, recipient, cancellationToken))
        {
            logger.LogInformation("Notification for event {EventId} to {Recipient} already sent; skipping", sourceEventId, recipient);
            return;
        }

        await emailSender.SendAsync(new EmailMessage(recipient, subject, body), cancellationToken);

        dbContext.AddNotification(Notification.Sent(sourceEventId, NotificationChannels.Email, recipient, subject, body, timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
