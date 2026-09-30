using BuildingBlocks.Results;

namespace Inventory.Domain;

/// <summary>
/// Stock held for one order, all lines or none. Keyed by OrderId (unique), which
/// is what makes reserving idempotent when the outbox redelivers OrderPlaced.
/// </summary>
public sealed class StockReservation
{
    private readonly List<ReservationLine> _lines = [];

    private StockReservation() { }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public ReservationStatus Status { get; private set; }
    public DateTimeOffset CreatedOnUtc { get; private set; }
    public DateTimeOffset? ReleasedOnUtc { get; private set; }
    public IReadOnlyList<ReservationLine> Lines => _lines;

    /// <summary>
    /// Reserves every requested line against <paramref name="stock"/> (keyed by
    /// normalised SKU), or nothing at all if any line is short.
    /// </summary>
    public static Result<StockReservation> TryReserve(
        Guid orderId,
        IReadOnlyCollection<ReservationLine> requested,
        IReadOnlyDictionary<string, StockItem> stock,
        DateTimeOffset now)
    {
        if (requested.Count == 0)
            throw new ArgumentException("A reservation needs at least one line.", nameof(requested));

        var totals = Consolidate(requested);
        var unavailable = FindShortages(totals, stock);

        if (unavailable.Count != 0)
        {
            return Result.Failure<StockReservation>(Error.Conflict(
                "Stock.Insufficient",
                $"Insufficient stock for: {string.Join(", ", unavailable)}."));
        }

        foreach (var line in totals)
        {
            stock[line.Sku].Reserve(line.Quantity, now);
        }

        var reservation = new StockReservation
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Status = ReservationStatus.Active,
            CreatedOnUtc = now,
        };
        reservation._lines.AddRange(totals);
        return reservation;
    }

    /// <summary>The normalised SKUs that do not have enough available stock for the requested lines.</summary>
    public static IReadOnlyList<string> FindShortages(IReadOnlyCollection<ReservationLine> requested, IReadOnlyDictionary<string, StockItem> stock)
        => Consolidate(requested)
            .Where(l => !stock.TryGetValue(l.Sku, out var item) || item.QuantityAvailable < l.Quantity)
            .Select(l => l.Sku)
            .ToList();

    // Merge duplicate SKUs so availability is checked against the total quantity.
    private static List<ReservationLine> Consolidate(IEnumerable<ReservationLine> lines)
        => lines
            .GroupBy(l => StockItem.NormalizeSku(l.Sku))
            .Select(g => new ReservationLine(g.Key, g.Sum(l => l.Quantity)))
            .ToList();

    /// <summary>Returns the reserved quantities to <paramref name="stock"/>. Idempotent.</summary>
    public void Release(IReadOnlyDictionary<string, StockItem> stock, DateTimeOffset now)
    {
        if (Status == ReservationStatus.Released)
            return;

        foreach (var line in _lines)
        {
            if (stock.TryGetValue(line.Sku, out var item))
            {
                item.Release(line.Quantity, now);
            }
        }

        Status = ReservationStatus.Released;
        ReleasedOnUtc = now;
    }
}

public sealed record ReservationLine(string Sku, int Quantity);

public enum ReservationStatus
{
    Active = 0,
    Released = 1,
}
