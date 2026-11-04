using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Results;
using BuildingBlocks.Security;
using Identity.Application.Abstractions;
using Identity.Application.Features.ListUsers;
using MediatR;

namespace Identity.Application.Features.DisableUser;

public sealed class DisableUserHandler(
    IIdentityDbContext dbContext,
    ITenantContext tenantContext,
    ICurrentUser currentUser)
    : IRequestHandler<DisableUserCommand, Result<UserResponse>>
{
    public async Task<Result<UserResponse>> Handle(DisableUserCommand request, CancellationToken cancellationToken)
    {
        // Identity's tables have no tenant query filter, so the tenant must be passed explicitly.
        var user = await dbContext.FindUserAsync(tenantContext.RequiredTenantId, request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<UserResponse>(Error.NotFound("Identity.UserNotFound", $"User '{request.UserId}' was not found."));
        }

        if (currentUser.UserId == user.Id)
        {
            return Result.Failure<UserResponse>(Error.Conflict("Identity.CannotDisableSelf", "You cannot disable your own account."));
        }

        var disabled = user.Disable();
        if (disabled.IsFailure)
        {
            return Result.Failure<UserResponse>(disabled.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return UserResponse.From(user);
    }
}
