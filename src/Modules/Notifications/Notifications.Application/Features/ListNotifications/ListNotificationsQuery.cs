using BuildingBlocks.Pagination;
using BuildingBlocks.Results;
using FluentValidation;
using MediatR;
using Notifications.Application.Abstractions;

namespace Notifications.Application.Features.ListNotifications;

public sealed record ListNotificationsQuery(int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IRequest<Result<PagedResult<NotificationResponse>>>;

public sealed class ListNotificationsValidator : AbstractValidator<ListNotificationsQuery>
{
    public ListNotificationsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

public sealed class ListNotificationsHandler(INotificationsDbContext dbContext)
    : IRequestHandler<ListNotificationsQuery, Result<PagedResult<NotificationResponse>>>
{
    public async Task<Result<PagedResult<NotificationResponse>>> Handle(ListNotificationsQuery request, CancellationToken cancellationToken)
    {
        var page = await dbContext.ListAsync(request.Page, request.PageSize, cancellationToken);
        return page.Map(NotificationResponse.From);
    }
}
