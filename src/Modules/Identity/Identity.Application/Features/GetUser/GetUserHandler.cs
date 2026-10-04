using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Results;
using Identity.Application.Abstractions;
using Identity.Application.Features.ListUsers;
using MediatR;

namespace Identity.Application.Features.GetUser;

public sealed class GetUserHandler(IIdentityDbContext dbContext, ITenantContext tenantContext)
    : IRequestHandler<GetUserQuery, Result<UserResponse>>
{
    public async Task<Result<UserResponse>> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        // Identity's tables have no tenant query filter, so the tenant must be passed explicitly.
        var user = await dbContext.FindUserAsync(tenantContext.RequiredTenantId, request.UserId, cancellationToken);

        return user is null
            ? Result.Failure<UserResponse>(Error.NotFound("Identity.UserNotFound", $"User '{request.UserId}' was not found."))
            : UserResponse.From(user);
    }
}
