using BuildingBlocks.Results;
using FluentValidation;
using MediatR;
using Reporting.Application.Abstractions;
using Reporting.Application.Features.GetSalesReport;

namespace Reporting.Application.Features.GetTopOrders;

/// <summary>
/// The largest orders by value over a UTC date range (defaults to the last 30 days,
/// inclusive of today, like the sales report). Cancelled orders are excluded.
/// </summary>
public sealed record GetTopOrdersQuery(DateOnly? From, DateOnly? To, int Limit = GetTopOrdersValidator.DefaultLimit)
    : IRequest<Result<TopOrdersResponse>>;

public sealed record TopOrdersResponse(DateOnly From, DateOnly To, IReadOnlyList<TopOrderRow> Orders);

public sealed class GetTopOrdersValidator : AbstractValidator<GetTopOrdersQuery>
{
    public const int DefaultLimit = 10;
    public const int MaxLimit = 100;

    public GetTopOrdersValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, MaxLimit);
        RuleFor(x => x)
            .Must(q => q.From is null || q.To is null || q.From <= q.To)
            .WithMessage("'from' must be on or before 'to'.")
            .Must(q => q.From is null || q.To is null || q.To.Value.DayNumber - q.From.Value.DayNumber < GetSalesReportValidator.MaxDays)
            .WithMessage($"The report range may span at most {GetSalesReportValidator.MaxDays} days.");
    }
}

public sealed class GetTopOrdersHandler(IReportingDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<GetTopOrdersQuery, Result<TopOrdersResponse>>
{
    public async Task<Result<TopOrdersResponse>> Handle(GetTopOrdersQuery request, CancellationToken cancellationToken)
    {
        var to = request.To ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var from = request.From ?? to.AddDays(-29);

        var orders = await dbContext.GetTopOrdersAsync(from, to, request.Limit, cancellationToken);

        return new TopOrdersResponse(from, to, orders);
    }
}
