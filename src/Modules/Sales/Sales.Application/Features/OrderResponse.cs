using BuildingBlocks.Results;
using Sales.Contracts.IntegrationEvents;
using Sales.Domain;

namespace Sales.Application.Features;

public sealed record OrderResponse(
    Guid Id,
    string CustomerName,
    string CustomerEmail,
    OrderStatus Status,
    decimal Total,
    DateTimeOffset PlacedOnUtc,
    DateTimeOffset? ConfirmedOnUtc,
    DateTimeOffset? CancelledOnUtc,
    string? RejectionReason,
    IReadOnlyList<OrderLineResponse> Lines)
{
    public static OrderResponse From(Order o) => new(
        o.Id, o.CustomerName, o.CustomerEmail, o.Status, o.Total, o.PlacedOnUtc, o.ConfirmedOnUtc, o.CancelledOnUtc, o.RejectionReason,
        o.Lines.Select(l => new OrderLineResponse(l.Sku, l.ProductName, l.Quantity, l.UnitPrice, l.LineTotal)).ToList());
}

public sealed record OrderLineResponse(string Sku, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record OrderSummaryResponse(Guid Id, string CustomerName, OrderStatus Status, decimal Total, int ItemCount, DateTimeOffset PlacedOnUtc)
{
    public static OrderSummaryResponse From(Order o)
        => new(o.Id, o.CustomerName, o.Status, o.Total, o.Lines.Sum(l => l.Quantity), o.PlacedOnUtc);
}

internal static class SalesMappings
{
    public static IReadOnlyList<OrderLineItem> ToLineItems(this Order order)
        => order.Lines.Select(l => new OrderLineItem(l.Sku, l.ProductName, l.Quantity, l.UnitPrice)).ToList();

    public static Error OrderNotFound(Guid orderId)
        => Error.NotFound("Orders.NotFound", $"Order '{orderId}' was not found.");
}
