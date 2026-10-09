using BuildingBlocks.Pagination;
using BuildingBlocks.Results;
using MediatR;

namespace Catalog.Application.Features.ListProducts;

/// <param name="MinPrice">When set, only products priced at or above this.</param>
/// <param name="MaxPrice">When set, only products priced at or below this.</param>
public sealed record ListProductsQuery(
    string? Search,
    bool IncludeInactive,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize)
    : IRequest<Result<PagedResult<ProductResponse>>>;
