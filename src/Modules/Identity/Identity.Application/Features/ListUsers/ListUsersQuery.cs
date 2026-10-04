using BuildingBlocks.Results;
using Identity.Domain;
using MediatR;

namespace Identity.Application.Features.ListUsers;

public sealed record ListUsersQuery : IRequest<Result<IReadOnlyList<UserResponse>>>;

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
