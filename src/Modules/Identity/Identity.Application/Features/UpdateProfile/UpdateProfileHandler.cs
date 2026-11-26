using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Results;
using BuildingBlocks.Security;
using Identity.Application.Abstractions;
using Identity.Application.Features.ListUsers;
using MediatR;

namespace Identity.Application.Features.UpdateProfile;

public sealed class UpdateProfileHandler(
    IIdentityDbContext dbContext,
    ITenantContext tenantContext,
    ICurrentUser currentUser)
    : IRequestHandler<UpdateProfileCommand, Result<UserResponse>>
{
    public async Task<Result<UserResponse>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        // Identity's tables have no tenant query filter, so the tenant must be passed explicitly.
        var user = currentUser.UserId is { } userId
            ? await dbContext.FindUserAsync(tenantContext.RequiredTenantId, userId, cancellationToken)
            : null;
        if (user is null)
        {
            return Result.Failure<UserResponse>(Error.NotFound("Identity.UserNotFound", "The signed-in user no longer exists."));
        }

        var renamed = user.Rename(request.DisplayName);
        if (renamed.IsFailure)
        {
            return Result.Failure<UserResponse>(renamed.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return UserResponse.From(user);
    }
}
