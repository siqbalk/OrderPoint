using BuildingBlocks.Results;
using FluentValidation;
using MediatR;
using Reporting.Application.Abstractions;

namespace Reporting.Application.Features.GetMonthlySales;

/// <summary>Sales per UTC calendar month of one year (defaults to the current year).</summary>
public sealed record GetMonthlySalesQuery(int? Year) : IRequest<Result<MonthlySalesResponse>>;

/// <param name="Months">All twelve months in order, including those with no sales.</param>
public sealed record MonthlySalesResponse(
    int Year,
    decimal Revenue,
    int ConfirmedOrders,
    int CancelledOrders,
    int ItemsSold,
    IReadOnlyList<MonthlySalesRow> Months);

public sealed class GetMonthlySalesValidator : AbstractValidator<GetMonthlySalesQuery>
{
    public const int MinYear = 2000;
    public const int MaxYear = 2100;

    public GetMonthlySalesValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(MinYear, MaxYear);
    }
}

public sealed class GetMonthlySalesHandler(IReportingDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<GetMonthlySalesQuery, Result<MonthlySalesResponse>>
{
    public async Task<Result<MonthlySalesResponse>> Handle(GetMonthlySalesQuery request, CancellationToken cancellationToken)
    {
        var year = request.Year ?? timeProvider.GetUtcNow().Year;

        var byMonth = (await dbContext.GetMonthlySalesAsync(year, cancellationToken)).ToDictionary(m => m.Month);
        var months = Enumerable.Range(1, 12)
            .Select(m => byMonth.GetValueOrDefault(m) ?? new MonthlySalesRow(m, 0, 0, 0m, 0))
            .ToList();

        return new MonthlySalesResponse(
            year,
            months.Sum(m => m.Revenue),
            months.Sum(m => m.ConfirmedOrders),
            months.Sum(m => m.CancelledOrders),
            months.Sum(m => m.ItemsSold),
            months);
    }
}
