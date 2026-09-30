using BuildingBlocks.Results;
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
    DateTimeOffset? LastLoginOnUtc);
