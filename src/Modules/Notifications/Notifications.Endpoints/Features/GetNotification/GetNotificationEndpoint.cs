using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Notifications.Application.Features;
using Notifications.Application.Features.GetNotification;
using Notifications.Contracts;

namespace Notifications.Endpoints.Features.GetNotification;

public static class GetNotificationEndpoint
{
    public static void MapGetNotification(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{notificationId:guid}", async (Guid notificationId, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetNotificationQuery(notificationId), cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("GetNotification")
        .Produces<NotificationResponse>()
        .RequireAuthorization(NotificationsPermissions.NotificationsRead);
    }
}
