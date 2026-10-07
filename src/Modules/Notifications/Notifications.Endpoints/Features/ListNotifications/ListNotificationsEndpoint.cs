using BuildingBlocks.Pagination;
using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Notifications.Application.Features;
using Notifications.Application.Features.ListNotifications;
using Notifications.Contracts;

namespace Notifications.Endpoints.Features.ListNotifications;

public static class ListNotificationsEndpoint
{
    public static void MapListNotifications(this IEndpointRouteBuilder app)
    {
        app.MapGet("", async (string? recipient, int? page, int? pageSize, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new ListNotificationsQuery(recipient, page ?? 1, pageSize ?? Paging.DefaultPageSize),
                cancellationToken);
            return result.ToHttpResult(Results.Ok);
        })
        .WithName("ListNotifications")
        .WithSummary("Sent notifications, newest first, optionally only those sent to one recipient")
        .Produces<PagedResult<NotificationResponse>>()
        .RequireAuthorization(NotificationsPermissions.NotificationsRead);
    }
}
