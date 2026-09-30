using BuildingBlocks.Results;
using MediatR;

namespace Sales.Application.Features.PlaceOrder;

/// <summary>
/// Places an order. Only SKUs and quantities come from the caller; names and
/// prices are read from Catalog so a client cannot set its own price.
/// </summary>
public sealed record PlaceOrderCommand(
    string CustomerName,
    string CustomerEmail,
    IReadOnlyList<PlaceOrderLine> Lines) : IRequest<Result<OrderResponse>>;

public sealed record PlaceOrderLine(string Sku, int Quantity);
