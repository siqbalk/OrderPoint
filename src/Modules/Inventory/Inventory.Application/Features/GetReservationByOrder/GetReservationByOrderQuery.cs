using BuildingBlocks.Results;
using FluentValidation;
using Inventory.Application.Abstractions;
using MediatR;

namespace Inventory.Application.Features.GetReservationByOrder;

/// <summary>The stock Inventory is holding (or held) for one order.</summary>
public sealed record GetReservationByOrderQuery(Guid OrderId) : IRequest<Result<StockReservationResponse>>;

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
