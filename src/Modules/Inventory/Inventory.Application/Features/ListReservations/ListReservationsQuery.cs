using BuildingBlocks.Pagination;
using BuildingBlocks.Results;
using FluentValidation;
using Inventory.Application.Abstractions;
using Inventory.Domain;
using MediatR;

namespace Inventory.Application.Features.ListReservations;

/// <summary>Stock reservations, newest first, e.g. to see which orders are holding a SKU's reserved units.</summary>
/// <param name="Status">When set, only reservations in this status (Active ones are still holding stock).</param>
/// <param name="Sku">When set, only reservations with a line for this SKU (case-insensitive).</param>
public sealed record ListReservationsQuery(
    ReservationStatus? Status,
    string? Sku = null,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize)
    : IRequest<Result<PagedResult<StockReservationResponse>>>;

public sealed class ListReservationsValidator : AbstractValidator<ListReservationsQuery>
{
    public ListReservationsValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Sku).MaximumLength(50);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}

public sealed class ListReservationsHandler(IInventoryDbContext dbContext)
    : IRequestHandler<ListReservationsQuery, Result<PagedResult<StockReservationResponse>>>
{
    public async Task<Result<PagedResult<StockReservationResponse>>> Handle(ListReservationsQuery request, CancellationToken cancellationToken)
    {
        var sku = string.IsNullOrWhiteSpace(request.Sku) ? null : StockItem.NormalizeSku(request.Sku);
        var page = await dbContext.ListReservationsAsync(request.Status, sku, request.Page, request.PageSize, cancellationToken);
        return page.Map(StockReservationResponse.From);
    }
}
