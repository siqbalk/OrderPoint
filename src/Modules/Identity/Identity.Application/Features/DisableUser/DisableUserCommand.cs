using BuildingBlocks.Results;
using Identity.Application.Features.ListUsers;
using MediatR;

namespace Identity.Application.Features.DisableUser;

/// <summary>
/// Revokes a user's access to the organisation. Access tokens already issued
/// stay valid until they expire; the user cannot sign in again.
/// </summary>
public sealed record DisableUserCommand(Guid UserId) : IRequest<Result<UserResponse>>;
