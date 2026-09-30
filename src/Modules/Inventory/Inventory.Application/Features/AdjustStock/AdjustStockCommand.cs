using BuildingBlocks.Results;
using MediatR;

namespace Inventory.Application.Features.AdjustStock;

/// <summary>Stocktake correction: sets on-hand to the physically counted quantity.</summary>
public sealed record AdjustStockCommand(string Sku, int QuantityOnHand) : IRequest<Result<StockItemResponse>>;
