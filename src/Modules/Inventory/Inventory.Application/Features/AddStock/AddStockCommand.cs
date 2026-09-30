using BuildingBlocks.Results;
using MediatR;

namespace Inventory.Application.Features.AddStock;

/// <summary>Goods received: increases on-hand quantity, opening a stock record for the SKU if there is none yet.</summary>
public sealed record AddStockCommand(string Sku, int Quantity) : IRequest<Result<StockItemResponse>>;
