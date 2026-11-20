using BuildingBlocks.Results;
using Inventory.Application.Abstractions;
using Inventory.Domain;
using MediatR;

namespace Inventory.Application.Features.WriteOffStock;

public sealed class WriteOffStockHandler(IInventoryDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<WriteOffStockCommand, Result<StockItemResponse>>
{
    public async Task<Result<StockItemResponse>> Handle(WriteOffStockCommand request, CancellationToken cancellationToken)
    {
        var sku = StockItem.NormalizeSku(request.Sku);
        var stockItem = await dbContext.FindBySkuAsync(sku, cancellationToken);
        if (stockItem is null)
        {
            return Result.Failure<StockItemResponse>(InventoryErrors.StockNotFound(sku));
        }

        var writtenOff = stockItem.WriteOff(request.Quantity, timeProvider.GetUtcNow());
        if (writtenOff.IsFailure)
        {
            return Result.Failure<StockItemResponse>(writtenOff.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return StockItemResponse.From(stockItem);
    }
}
