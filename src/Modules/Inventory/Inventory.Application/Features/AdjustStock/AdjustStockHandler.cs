using BuildingBlocks.Results;
using Inventory.Application.Abstractions;
using Inventory.Domain;
using MediatR;

namespace Inventory.Application.Features.AdjustStock;

public sealed class AdjustStockHandler(IInventoryDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<AdjustStockCommand, Result<StockItemResponse>>
{
    public async Task<Result<StockItemResponse>> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        var sku = StockItem.NormalizeSku(request.Sku);
        var stockItem = await dbContext.FindBySkuAsync(sku, cancellationToken);
        if (stockItem is null)
        {
            return Result.Failure<StockItemResponse>(InventoryErrors.StockNotFound(sku));
        }

        var adjusted = stockItem.AdjustTo(request.QuantityOnHand, timeProvider.GetUtcNow());
        if (adjusted.IsFailure)
        {
            return Result.Failure<StockItemResponse>(adjusted.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return StockItemResponse.From(stockItem);
    }
}
