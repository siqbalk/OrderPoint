using BuildingBlocks.Results;
using FluentValidation;
using MediatR;
using Sales.Application.Abstractions;
using Sales.Domain;

namespace Sales.Application.Features.GetCustomerSummary;

/// <summary>One customer's order history at a glance, looked up by email (case-insensitive).</summary>
public sealed record GetCustomerSummaryQuery(string Email) : IRequest<Result<CustomerSummaryResponse>>;

/// <param name="TotalSpent">Total of confirmed orders; placed, rejected and cancelled orders are not counted.</param>
/// <param name="ByStatus">Every status, in lifecycle order, including those with zero orders.</param>
public sealed record CustomerSummaryResponse(
    string CustomerEmail,
    int TotalOrders,
    decimal TotalSpent,
    DateTimeOffset FirstOrderOnUtc,
    DateTimeOffset LastOrderOnUtc,
    IReadOnlyList<OrderStatusCount> ByStatus);

public sealed class GetCustomerSummaryValidator : AbstractValidator<GetCustomerSummaryQuery>
{
    public GetCustomerSummaryValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256);
    }
}

public sealed class GetCustomerSummaryHandler(ISalesDbContext dbContext)
    : IRequestHandler<GetCustomerSummaryQuery, Result<CustomerSummaryResponse>>
{
    public async Task<Result<CustomerSummaryResponse>> Handle(GetCustomerSummaryQuery request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var totals = (await dbContext.GetCustomerOrderTotalsAsync(email, cancellationToken)).ToDictionary(t => t.Status);
        if (totals.Count == 0)
        {
            return Result.Failure<CustomerSummaryResponse>(
                Error.NotFound("Orders.CustomerNotFound", $"No orders were found for '{email}'."));
        }

        var byStatus = Enum.GetValues<OrderStatus>()
            .Select(s => new OrderStatusCount(s, totals.GetValueOrDefault(s)?.Count ?? 0))
            .ToList();

        return new CustomerSummaryResponse(
            email,
            byStatus.Sum(c => c.Count),
            totals.GetValueOrDefault(OrderStatus.Confirmed)?.Total ?? 0m,
            totals.Values.Min(t => t.FirstPlacedOnUtc),
            totals.Values.Max(t => t.LastPlacedOnUtc),
            byStatus);
    }
}
