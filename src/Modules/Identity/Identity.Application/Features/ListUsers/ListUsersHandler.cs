using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Results;
using Identity.Application.Abstractions;
using MediatR;

namespace Identity.Application.Features.ListUsers;

public sealed class ListUsersHandler(IIdentityDbContext dbContext, ITenantContext tenantContext)
    : IRequestHandler<ListUsersQuery, Result<IReadOnlyList<UserResponse>>>
{
    public async Task<Result<IReadOnlyList<UserResponse>>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await dbContext.ListUsersAsync(tenantContext.RequiredTenantId, cancellationToken);

        return users
            .Select(u => new UserResponse(u.Id, u.Email, u.DisplayName, u.Role.ToString(), u.Status.ToString(), u.CreatedOnUtc, u.LastLoginOnUtc))
            .ToList();
    }
}
