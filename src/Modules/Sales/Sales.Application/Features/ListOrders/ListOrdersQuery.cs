using BuildingBlocks.Pagination;
using BuildingBlocks.Results;
using FluentValidation;
using MediatR;
using Sales.Application.Abstractions;
using Sales.Domain;

namespace Sales.Application.Features.ListOrders;

/// <param name="CustomerEmail">When set, only orders placed for this address (case-insensitive), e.g. to see one customer's order history.</param>
/// <param name="From">When set, only orders placed on or after this UTC date.</param>
/// <param name="To">When set, only orders placed on or before this UTC date.</param>
public sealed record ListOrdersQuery(
    OrderStatus? Status,
    string? CustomerEmail = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize)
    : IRequest<Result<PagedResult<OrderSummaryResponse>>>;

public sealed class ListOrdersValidator : AbstractValidator<ListOrdersQuery>
{
    public ListOrdersValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.CustomerEmail).MaximumLength(256);
        RuleFor(x => x)
            .Must(q => q.From is null || q.To is null || q.From <= q.To)
            .WithMessage("'from' must be on or before 'to'.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

public sealed class ListOrdersHandler(ISalesDbContext dbContext)
    : IRequestHandler<ListOrdersQuery, Result<PagedResult<OrderSummaryResponse>>>
{
    public async Task<Result<PagedResult<OrderSummaryResponse>>> Handle(ListOrdersQuery request, CancellationToken cancellationToken)
    {
        // Whole UTC days: [From 00:00, To + 1 day 00:00).
        DateTimeOffset? placedFromUtc = request.From is { } from ? new(from, TimeOnly.MinValue, TimeSpan.Zero) : null;
        DateTimeOffset? placedBeforeUtc = request.To is { } to ? new(to.AddDays(1), TimeOnly.MinValue, TimeSpan.Zero) : null;

        var page = await dbContext.ListOrdersAsync(
            request.Status, request.CustomerEmail, placedFromUtc, placedBeforeUtc, request.Page, request.PageSize, cancellationToken);
        return page.Map(OrderSummaryResponse.From);
    }
}
