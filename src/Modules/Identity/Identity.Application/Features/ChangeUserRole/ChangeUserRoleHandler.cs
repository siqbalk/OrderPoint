using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Results;
using BuildingBlocks.Security;
using Identity.Application.Abstractions;
using Identity.Application.Features.ListUsers;
using MediatR;

namespace Identity.Application.Features.ChangeUserRole;

public sealed class ChangeUserRoleHandler(
    IIdentityDbContext dbContext,
    ITenantContext tenantContext,
    ICurrentUser currentUser)
    : IRequestHandler<ChangeUserRoleCommand, Result<UserResponse>>
{
    public async Task<Result<UserResponse>> Handle(ChangeUserRoleCommand request, CancellationToken cancellationToken)
    {
        // Identity's tables have no tenant query filter, so the tenant must be passed explicitly.
        var user = await dbContext.FindUserAsync(tenantContext.RequiredTenantId, request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<UserResponse>(Error.NotFound("Identity.UserNotFound", $"User '{request.UserId}' was not found."));
        }

        if (currentUser.UserId == user.Id)
        {
            return Result.Failure<UserResponse>(Error.Conflict("Identity.CannotChangeOwnRole", "You cannot change your own role."));
        }

        var changed = user.ChangeRole(request.Role);
        if (changed.IsFailure)
        {
            return Result.Failure<UserResponse>(changed.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return UserResponse.From(user);
    }
}
