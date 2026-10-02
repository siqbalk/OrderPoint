using BuildingBlocks.Results;
using FluentValidation;
using MediatR;
using Notifications.Application.Abstractions;

namespace Notifications.Application.Features.GetNotification;

/// <summary>One entry from the tenant's sent-email log, including its full body.</summary>
public sealed record GetNotificationQuery(Guid NotificationId) : IRequest<Result<NotificationResponse>>;

public sealed class GetNotificationValidator : AbstractValidator<GetNotificationQuery>
{
    public GetNotificationValidator()
    {
        RuleFor(x => x.NotificationId).NotEmpty();
    }
}

public sealed class GetNotificationHandler(INotificationsDbContext dbContext)
    : IRequestHandler<GetNotificationQuery, Result<NotificationResponse>>
{
    public async Task<Result<NotificationResponse>> Handle(GetNotificationQuery request, CancellationToken cancellationToken)
    {
        var notification = await dbContext.FindByIdAsync(request.NotificationId, cancellationToken);

        return notification is null
            ? Result.Failure<NotificationResponse>(NotificationsErrors.NotificationNotFound(request.NotificationId))
            : NotificationResponse.From(notification);
    }
}
