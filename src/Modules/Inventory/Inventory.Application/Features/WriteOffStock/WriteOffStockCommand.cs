using BuildingBlocks.Results;
using MediatR;

namespace Inventory.Application.Features.WriteOffStock;

/// <summary>
/// Removes damaged, lost or expired units from on-hand. Unlike a stocktake
/// adjustment it is relative, so it cannot overwrite a concurrent delivery.
/// </summary>
public sealed record WriteOffStockCommand(string Sku, int Quantity) : IRequest<Result<StockItemResponse>>;
