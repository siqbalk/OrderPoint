using BuildingBlocks.Pagination;
using Notifications.Domain;

namespace Notifications.Application.Abstractions;

public interface INotificationsDbContext
{
    void AddNotification(Notification notification);

    Task<bool> ExistsAsync(Guid sourceEventId, string recipient, CancellationToken cancellationToken);

    Task<Notification?> FindByIdAsync(Guid notificationId, CancellationToken cancellationToken);

    /// <param name="recipient">When set, only notifications sent to this address (case-insensitive exact match).</param>
    Task<PagedResult<Notification>> ListAsync(string? recipient, int page, int pageSize, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Delivery channel. The default implementation only logs; swap in SMTP, SendGrid, SES, etc.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public sealed record EmailMessage(string To, string Subject, string Body);

/// <summary>The "Notifications" configuration section.</summary>
public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Public URL of the web app, used to build links in emails.</summary>
    public string AppBaseUrl { get; set; } = "http://localhost:5079";

    public string FromAddress { get; set; } = "no-reply@orderpoint.local";
}
