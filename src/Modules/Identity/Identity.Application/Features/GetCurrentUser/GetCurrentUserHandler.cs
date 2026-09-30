using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Results;
using BuildingBlocks.Security;
using Identity.Application.Abstractions;
using MediatR;

namespace Identity.Application.Features.GetCurrentUser;

public sealed class GetCurrentUserHandler(
    IIdentityDbContext dbContext,
    ICurrentUser currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<GetCurrentUserQuery, Result<CurrentUserResponse>>
{
    public async Task<Result<CurrentUserResponse>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        var user = currentUser.UserId is { } userId
            ? await dbContext.FindUserAsync(tenantId, userId, cancellationToken)
            : null;
        var tenant = await dbContext.FindTenantAsync(tenantId, cancellationToken);

        if (user is null || tenant is null)
        {
            return Result.Failure<CurrentUserResponse>(
                Error.NotFound("Identity.UserNotFound", "The signed-in user no longer exists."));
        }

        var userCount = await dbContext.CountUsersAsync(tenantId, cancellationToken);
        var limits = tenant.Limits;

        return new CurrentUserResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role.ToString(),
            new CurrentTenantResponse(tenant.Id, tenant.Name, tenant.Plan.ToString(), limits.MaxUsers, limits.MaxProducts, userCount),
            currentUser.Permissions);
    }
}
