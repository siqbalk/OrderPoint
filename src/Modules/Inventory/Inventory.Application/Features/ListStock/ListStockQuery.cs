using BuildingBlocks.Pagination;
using BuildingBlocks.Results;
using FluentValidation;
using Inventory.Application.Abstractions;
using MediatR;

namespace Inventory.Application.Features.ListStock;

/// <param name="MaxAvailable">When set, only SKUs with at most this many units available (a low-stock report).</param>
public sealed record ListStockQuery(int? MaxAvailable, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IRequest<Result<PagedResult<StockItemResponse>>>;

public sealed class ListStockValidator : AbstractValidator<ListStockQuery>
{
    public ListStockValidator()
    {
        RuleFor(x => x.MaxAvailable).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

public sealed class ListStockHandler(IInventoryDbContext dbContext)
    : IRequestHandler<ListStockQuery, Result<PagedResult<StockItemResponse>>>
{
    public async Task<Result<PagedResult<StockItemResponse>>> Handle(ListStockQuery request, CancellationToken cancellationToken)
    {
        var page = await dbContext.ListAsync(request.MaxAvailable, request.Page, request.PageSize, cancellationToken);
        return page.Map(StockItemResponse.From);
    }
}
