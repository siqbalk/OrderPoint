using BuildingBlocks.Results;
using BuildingBlocks.Security;
using Identity.Domain;
using MediatR;

namespace Identity.Application.Features.ListUsers;

/// <param name="Role">When set, only users with this role.</param>
/// <param name="Status">When set, only users in this status, e.g. <see cref="UserStatus.Invited"/> for pending invitations.</param>
public sealed record ListUsersQuery(TenantRole? Role = null, UserStatus? Status = null) : IRequest<Result<IReadOnlyList<UserResponse>>>;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    string Status,
    DateTimeOffset CreatedOnUtc,
    DateTimeOffset? LastLoginOnUtc)
{
    public static UserResponse From(User u)
        => new(u.Id, u.Email, u.DisplayName, u.Role.ToString(), u.Status.ToString(), u.CreatedOnUtc, u.LastLoginOnUtc);
}
