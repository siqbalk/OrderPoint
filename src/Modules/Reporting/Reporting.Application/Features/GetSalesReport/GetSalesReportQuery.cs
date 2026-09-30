using BuildingBlocks.Results;
using FluentValidation;
using MediatR;
using Reporting.Application.Abstractions;

namespace Reporting.Application.Features.GetSalesReport;

/// <summary>Sales summary over a UTC date range (defaults to the last 30 days, inclusive of today).</summary>
public sealed record GetSalesReportQuery(DateOnly? From, DateOnly? To) : IRequest<Result<SalesReportResponse>>;

public sealed record SalesReportResponse(
    DateOnly From,
    DateOnly To,
    decimal Revenue,
    int ConfirmedOrders,
    int CancelledOrders,
    decimal AverageOrderValue,
    int ItemsSold,
    IReadOnlyList<DailySalesRow> Days);

public sealed class GetSalesReportValidator : AbstractValidator<GetSalesReportQuery>
{
    public const int MaxDays = 366;

    public GetSalesReportValidator()
    {
        RuleFor(x => x)
            .Must(q => q.From is null || q.To is null || q.From <= q.To)
            .WithMessage("'from' must be on or before 'to'.")
            .Must(q => q.From is null || q.To is null || q.To.Value.DayNumber - q.From.Value.DayNumber < MaxDays)
            .WithMessage($"The report range may span at most {MaxDays} days.");
    }
}

public sealed class GetSalesReportHandler(IReportingDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<GetSalesReportQuery, Result<SalesReportResponse>>
{
    public async Task<Result<SalesReportResponse>> Handle(GetSalesReportQuery request, CancellationToken cancellationToken)
    {
        var to = request.To ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var from = request.From ?? to.AddDays(-29);

        var days = await dbContext.GetDailySalesAsync(from, to, cancellationToken);

        // Each confirmed order counts once in ConfirmedOrders; CancelledOrders is the subset later cancelled.
        var revenue = days.Sum(d => d.Revenue);
        var confirmed = days.Sum(d => d.ConfirmedOrders);
        var cancelled = days.Sum(d => d.CancelledOrders);
        var kept = confirmed - cancelled;

        return new SalesReportResponse(
            from,
            to,
            revenue,
            confirmed,
            cancelled,
            kept == 0 ? 0 : decimal.Round(revenue / kept, 2),
            days.Sum(d => d.ItemsSold),
            days);
    }
}
