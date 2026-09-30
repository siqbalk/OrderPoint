using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Abstractions;

namespace Notifications.Infrastructure.Email;

/// <summary>
/// Development email sender: writes the message to the log instead of sending
/// it. Replace the <see cref="IEmailSender"/> registration in NotificationsModule
/// with an SMTP/SendGrid/SES implementation for production.
/// </summary>
public sealed class LoggingEmailSender(IOptions<NotificationsOptions> options, ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Email from {From} to {To}: {Subject}{NewLine}{Body}",
            options.Value.FromAddress, message.To, message.Subject, Environment.NewLine, message.Body);

        return Task.CompletedTask;
    }
}
