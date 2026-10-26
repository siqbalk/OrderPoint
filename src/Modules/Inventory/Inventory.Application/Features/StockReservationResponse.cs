using Inventory.Domain;

namespace Inventory.Application.Features;

public sealed record StockReservationResponse(
    Guid Id,
    Guid OrderId,
    ReservationStatus Status,
    IReadOnlyList<ReservationLineResponse> Lines,
    DateTimeOffset CreatedOnUtc,
    DateTimeOffset? ReleasedOnUtc)
{
    public static StockReservationResponse From(StockReservation r)
        => new(
            r.Id,
            r.OrderId,
            r.Status,
            r.Lines.Select(l => new ReservationLineResponse(l.Sku, l.Quantity)).ToList(),
            r.CreatedOnUtc,
            r.ReleasedOnUtc);
}

public sealed record ReservationLineResponse(string Sku, int Quantity);
