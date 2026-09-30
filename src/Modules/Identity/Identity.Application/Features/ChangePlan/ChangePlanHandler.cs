using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Results;
using Identity.Application.Abstractions;
using Identity.Domain;
using MediatR;

namespace Identity.Application.Features.ChangePlan;

public sealed class ChangePlanHandler(
    IIdentityDbContext dbContext,
    ITenantContext tenantContext,
    TimeProvider timeProvider)
    : IRequestHandler<ChangePlanCommand, Result<ChangePlanResponse>>
{
    public async Task<Result<ChangePlanResponse>> Handle(ChangePlanCommand request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.FindTenantAsync(tenantContext.RequiredTenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<ChangePlanResponse>(Error.NotFound("Identity.TenantNotFound", "Tenant not found."));
        }

        var newLimits = PlanLimits.For(request.Plan);
        if (newLimits.MaxUsers is { } maxUsers && await dbContext.CountUsersAsync(tenant.Id, cancellationToken) > maxUsers)
        {
            return Result.Failure<ChangePlanResponse>(Error.Conflict(
                "Identity.PlanDowngradeBlocked",
                $"The {request.Plan} plan allows at most {maxUsers} users; remove users before downgrading."));
        }

        tenant.ChangePlan(request.Plan, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ChangePlanResponse(tenant.Id, tenant.Plan.ToString(), newLimits.MaxUsers, newLimits.MaxProducts);
    }
}
