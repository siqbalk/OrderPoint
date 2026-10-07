using BuildingBlocks.Pagination;
using BuildingBlocks.Results;
using FluentValidation;
using MediatR;
using Notifications.Application.Abstractions;

namespace Notifications.Application.Features.ListNotifications;

/// <param name="Recipient">When set, only notifications sent to this address (case-insensitive), e.g. to see what one customer was sent.</param>
public sealed record ListNotificationsQuery(string? Recipient = null, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IRequest<Result<PagedResult<NotificationResponse>>>;

public sealed class ListNotificationsValidator : AbstractValidator<ListNotificationsQuery>
{
    public ListNotificationsValidator()
    {
        RuleFor(x => x.Recipient).MaximumLength(256);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

public sealed class ListNotificationsHandler(INotificationsDbContext dbContext)
    : IRequestHandler<ListNotificationsQuery, Result<PagedResult<NotificationResponse>>>
{
    public async Task<Result<PagedResult<NotificationResponse>>> Handle(ListNotificationsQuery request, CancellationToken cancellationToken)
    {
        var page = await dbContext.ListAsync(request.Recipient, request.Page, request.PageSize, cancellationToken);
        return page.Map(NotificationResponse.From);
    }
}
