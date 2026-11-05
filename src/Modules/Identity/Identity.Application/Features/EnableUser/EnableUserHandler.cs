using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Results;
using Identity.Application.Abstractions;
using Identity.Application.Features.ListUsers;
using Identity.Domain;
using MediatR;

namespace Identity.Application.Features.EnableUser;

public sealed class EnableUserHandler(IIdentityDbContext dbContext, ITenantContext tenantContext)
    : IRequestHandler<EnableUserCommand, Result<UserResponse>>
{
    public async Task<Result<UserResponse>> Handle(EnableUserCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;

        // Identity's tables have no tenant query filter, so the tenant must be passed explicitly.
        var user = await dbContext.FindUserAsync(tenantId, request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<UserResponse>(Error.NotFound("Identity.UserNotFound", $"User '{request.UserId}' was not found."));
        }

        var wasDisabled = user.Status == UserStatus.Disabled;
        var enabled = user.Enable();
        if (enabled.IsFailure)
        {
            return Result.Failure<UserResponse>(enabled.Error);
        }

        // Disabled users don't count towards the plan's user limit, so re-enabling one takes a seat back.
        // The count reads the database, where this user is still disabled; nothing is saved if it fails.
        if (wasDisabled)
        {
            var tenant = await dbContext.FindTenantAsync(tenantId, cancellationToken);
            if (tenant?.Limits.MaxUsers is { } maxUsers && await dbContext.CountUsersAsync(tenantId, cancellationToken) >= maxUsers)
            {
                return Result.Failure<UserResponse>(Error.Forbidden(
                    "Identity.PlanLimitReached",
                    $"The {tenant.Plan} plan allows at most {maxUsers} users. Upgrade the plan or disable another user first."));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return UserResponse.From(user);
    }
}
