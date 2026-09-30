using BuildingBlocks.Results;
using Inventory.Application.Abstractions;
using Inventory.Domain;
using MediatR;

namespace Inventory.Application.Features.GetStockBySku;

public sealed record GetStockBySkuQuery(string Sku) : IRequest<Result<StockItemResponse>>;

public sealed class GetStockBySkuHandler(IInventoryDbContext dbContext) : IRequestHandler<GetStockBySkuQuery, Result<StockItemResponse>>
{
    public async Task<Result<StockItemResponse>> Handle(GetStockBySkuQuery request, CancellationToken cancellationToken)
    {
        var sku = StockItem.NormalizeSku(request.Sku);
        var stockItem = await dbContext.FindBySkuAsync(sku, cancellationToken);

        return stockItem is null
            ? Result.Failure<StockItemResponse>(InventoryErrors.StockNotFound(sku))
            : StockItemResponse.From(stockItem);
    }
}
