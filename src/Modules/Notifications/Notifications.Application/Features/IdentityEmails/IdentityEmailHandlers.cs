using Identity.Contracts.IntegrationEvents;
using MediatR;
using Microsoft.Extensions.Options;
using Notifications.Application.Abstractions;

namespace Notifications.Application.Features.IdentityEmails;

public sealed class SendWelcomeEmailOnTenantRegistered(EmailDispatcher dispatcher, IOptions<NotificationsOptions> options)
    : INotificationHandler<TenantRegisteredIntegrationEvent>
{
    public Task Handle(TenantRegisteredIntegrationEvent notification, CancellationToken cancellationToken)
        => dispatcher.SendAsync(
            notification.EventId,
            notification.OwnerEmail,
            $"Welcome to OrderPoint, {notification.TenantName}!",
            $"""
            Hi {notification.OwnerDisplayName},

            Your organisation "{notification.TenantName}" is ready on the Free plan.
            Sign in at {options.Value.AppBaseUrl} to add products and invite your team.
            """,
            cancellationToken);
}

public sealed class SendInvitationEmailOnUserInvited(EmailDispatcher dispatcher, IOptions<NotificationsOptions> options)
    : INotificationHandler<UserInvitedIntegrationEvent>
{
    public Task Handle(UserInvitedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        var acceptUrl = $"{options.Value.AppBaseUrl.TrimEnd('/')}/accept-invitation?token={Uri.EscapeDataString(notification.InvitationToken)}";

        return dispatcher.SendAsync(
            notification.EventId,
            notification.Email,
            $"You've been invited to {notification.TenantName}",
            $"""
            Hi {notification.DisplayName},

            You have been invited to join {notification.TenantName} as {notification.Role}.
            Accept the invitation and choose a password here (valid until {notification.ExpiresOnUtc:u}):

            {acceptUrl}
            """,
            cancellationToken);
    }
}
