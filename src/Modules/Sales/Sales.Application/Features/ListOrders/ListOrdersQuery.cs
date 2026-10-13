using BuildingBlocks.Pagination;
using BuildingBlocks.Results;
using FluentValidation;
using MediatR;
using Sales.Application.Abstractions;
using Sales.Domain;

namespace Sales.Application.Features.ListOrders;

/// <param name="CustomerEmail">When set, only orders placed for this address (case-insensitive), e.g. to see one customer's order history.</param>
public sealed record ListOrdersQuery(OrderStatus? Status, string? CustomerEmail = null, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IRequest<Result<PagedResult<OrderSummaryResponse>>>;

public sealed class ListOrdersValidator : AbstractValidator<ListOrdersQuery>
{
    public ListOrdersValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.CustomerEmail).MaximumLength(256);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

public sealed class ListOrdersHandler(ISalesDbContext dbContext)
    : IRequestHandler<ListOrdersQuery, Result<PagedResult<OrderSummaryResponse>>>
{
    public async Task<Result<PagedResult<OrderSummaryResponse>>> Handle(ListOrdersQuery request, CancellationToken cancellationToken)
    {
        var page = await dbContext.ListOrdersAsync(request.Status, request.CustomerEmail, request.Page, request.PageSize, cancellationToken);
        return page.Map(OrderSummaryResponse.From);
    }
}
