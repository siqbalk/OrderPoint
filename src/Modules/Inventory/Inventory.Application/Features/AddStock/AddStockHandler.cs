using BuildingBlocks.Results;
using Inventory.Application.Abstractions;
using Inventory.Domain;
using MediatR;

namespace Inventory.Application.Features.AddStock;

public sealed class AddStockHandler(IInventoryDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<AddStockCommand, Result<StockItemResponse>>
{
    public async Task<Result<StockItemResponse>> Handle(AddStockCommand request, CancellationToken cancellationToken)
    {
        var stockItem = await dbContext.GetOrCreateBySkuAsync(StockItem.NormalizeSku(request.Sku), cancellationToken);

        stockItem.Receive(request.Quantity, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        return StockItemResponse.From(stockItem);
    }
}
