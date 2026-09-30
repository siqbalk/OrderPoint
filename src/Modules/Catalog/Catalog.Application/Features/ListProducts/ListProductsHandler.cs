using BuildingBlocks.Pagination;
using BuildingBlocks.Results;
using Catalog.Application.Abstractions;
using MediatR;

namespace Catalog.Application.Features.ListProducts;

public sealed class ListProductsHandler(ICatalogDbContext dbContext)
    : IRequestHandler<ListProductsQuery, Result<PagedResult<ProductResponse>>>
{
    public async Task<Result<PagedResult<ProductResponse>>> Handle(ListProductsQuery request, CancellationToken cancellationToken)
    {
        var page = await dbContext.ListAsync(request.Search, request.IncludeInactive, request.Page, request.PageSize, cancellationToken);
        return page.Map(ProductResponse.From);
    }
}
