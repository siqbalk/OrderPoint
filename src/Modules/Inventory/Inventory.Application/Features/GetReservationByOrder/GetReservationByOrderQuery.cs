using BuildingBlocks.Results;
using FluentValidation;
using Inventory.Application.Abstractions;
using Inventory.Domain;
using MediatR;

namespace Inventory.Application.Features.GetReservationByOrder;

/// <summary>The stock Inventory is holding (or held) for one order.</summary>
public sealed record GetReservationByOrderQuery(Guid OrderId) : IRequest<Result<StockReservationResponse>>;

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

public sealed class GetReservationByOrderValidator : AbstractValidator<GetReservationByOrderQuery>
{
    public GetReservationByOrderValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

public sealed class GetReservationByOrderHandler(IInventoryDbContext dbContext)
    : IRequestHandler<GetReservationByOrderQuery, Result<StockReservationResponse>>
{
    public async Task<Result<StockReservationResponse>> Handle(GetReservationByOrderQuery request, CancellationToken cancellationToken)
    {
        var reservation = await dbContext.FindReservationByOrderAsync(request.OrderId, cancellationToken);

        return reservation is null
            ? Result.Failure<StockReservationResponse>(InventoryErrors.ReservationNotFound(request.OrderId))
            : StockReservationResponse.From(reservation);
    }
}
