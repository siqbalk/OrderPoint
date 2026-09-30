using BuildingBlocks.Results;

namespace Sales.Domain;

/// <summary>
/// A customer order. Its lifecycle is driven partly by the user (place, cancel)
/// and partly by Inventory's asynchronous reply (confirm, reject):
/// <code>
///   Placed ──stock reserved──▶ Confirmed ──cancel──▶ Cancelled
///     │  └────────cancel─────────────────────────────▲
///     └──stock unavailable──▶ Rejected
/// </code>
/// Transitions that arrive late or twice (outbox redelivery) are idempotent.
/// </summary>
public sealed class Order
{
    private readonly List<OrderLine> _lines = [];

    private Order() { }

    public Guid Id { get; private set; }
    public string CustomerName { get; private set; } = string.Empty;
    public string CustomerEmail { get; private set; } = string.Empty;
    public OrderStatus Status { get; private set; }
    public decimal Total { get; private set; }
    public DateTimeOffset PlacedOnUtc { get; private set; }
    public DateTimeOffset? ConfirmedOnUtc { get; private set; }
    public DateTimeOffset? CancelledOnUtc { get; private set; }
    public string? RejectionReason { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _lines;

    public static Order Place(string customerName, string customerEmail, IReadOnlyCollection<OrderLineDraft> lines, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(customerName))
            throw new ArgumentException("Customer name is required.", nameof(customerName));
        if (string.IsNullOrWhiteSpace(customerEmail))
            throw new ArgumentException("Customer email is required.", nameof(customerEmail));
        if (lines.Count == 0)
            throw new ArgumentException("An order needs at least one line.", nameof(lines));

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerName = customerName.Trim(),
            CustomerEmail = customerEmail.Trim(),
            Status = OrderStatus.Placed,
            PlacedOnUtc = now,
        };

        order._lines.AddRange(lines.Select(OrderLine.From));
        order.Total = order._lines.Sum(l => l.LineTotal);
        return order;
    }

    public Result Confirm(DateTimeOffset now)
    {
        switch (Status)
        {
            case OrderStatus.Confirmed:
                return Result.Success();
            case OrderStatus.Placed:
                Status = OrderStatus.Confirmed;
                ConfirmedOnUtc = now;
                return Result.Success();
            default:
                return Result.Failure(InvalidTransition("confirm"));
        }
    }

    public Result Reject(string reason, DateTimeOffset now)
    {
        switch (Status)
        {
            case OrderStatus.Rejected:
                return Result.Success();
            case OrderStatus.Placed:
                Status = OrderStatus.Rejected;
                RejectionReason = reason;
                return Result.Success();
            default:
                return Result.Failure(InvalidTransition("reject"));
        }
    }

    public Result Cancel(DateTimeOffset now)
    {
        if (Status is not (OrderStatus.Placed or OrderStatus.Confirmed))
            return Result.Failure(InvalidTransition("cancel"));

        Status = OrderStatus.Cancelled;
        CancelledOnUtc = now;
        return Result.Success();
    }

    private Error InvalidTransition(string action)
        => Error.Conflict("Orders.InvalidStatus", $"Cannot {action} an order that is {Status}.");
}

public sealed class OrderLine
{
    private OrderLine() { }

    public Guid Id { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;

    internal static OrderLine From(OrderLineDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Sku))
            throw new ArgumentException("SKU is required.", nameof(draft));
        if (draft.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(draft), "Quantity must be positive.");
        if (draft.UnitPrice <= 0)
            throw new ArgumentOutOfRangeException(nameof(draft), "Unit price must be positive.");

        return new OrderLine
        {
            Id = Guid.NewGuid(),
            Sku = draft.Sku,
            ProductName = draft.ProductName,
            Quantity = draft.Quantity,
            UnitPrice = draft.UnitPrice,
        };
    }
}

/// <summary>A priced line ready to be placed — prices come from Catalog, never from the client.</summary>
public sealed record OrderLineDraft(string Sku, string ProductName, int Quantity, decimal UnitPrice);

public enum OrderStatus
{
    Placed = 0,
    Confirmed = 1,
    Rejected = 2,
    Cancelled = 3,
}
