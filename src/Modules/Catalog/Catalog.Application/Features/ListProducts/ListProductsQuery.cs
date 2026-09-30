using BuildingBlocks.Pagination;
using BuildingBlocks.Results;
using MediatR;

namespace Catalog.Application.Features.ListProducts;

public sealed record ListProductsQuery(string? Search, bool IncludeInactive, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IRequest<Result<PagedResult<ProductResponse>>>;
