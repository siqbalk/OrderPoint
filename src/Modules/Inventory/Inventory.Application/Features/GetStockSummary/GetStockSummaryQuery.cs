using BuildingBlocks.Results;
using Inventory.Application.Abstractions;
using MediatR;

namespace Inventory.Application.Features.GetStockSummary;

/// <summary>Stock totals across all of the tenant's SKUs, e.g. for a dashboard.</summary>
public sealed record GetStockSummaryQuery : IRequest<Result<StockSummaryResponse>>;

/// <param name="OutOfStockCount">SKUs with no units available to sell.</param>
public sealed record StockSummaryResponse(
    int SkuCount,
    long QuantityOnHand,
    long QuantityReserved,
    long QuantityAvailable,
    int OutOfStockCount);

public sealed class GetStockSummaryHandler(IInventoryDbContext dbContext)
    : IRequestHandler<GetStockSummaryQuery, Result<StockSummaryResponse>>
{
    public async Task<Result<StockSummaryResponse>> Handle(GetStockSummaryQuery request, CancellationToken cancellationToken)
    {
        var totals = await dbContext.GetStockTotalsAsync(cancellationToken);

        return new StockSummaryResponse(
            totals.SkuCount,
            totals.QuantityOnHand,
            totals.QuantityReserved,
            totals.QuantityOnHand - totals.QuantityReserved,
            totals.OutOfStockCount);
    }
}
