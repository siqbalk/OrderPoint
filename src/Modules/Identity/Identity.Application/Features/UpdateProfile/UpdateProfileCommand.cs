using BuildingBlocks.Results;
using Identity.Application.Features.ListUsers;
using MediatR;

namespace Identity.Application.Features.UpdateProfile;

/// <summary>
/// The signed-in user changes their own display name. Any active user may do
/// this for themselves; there is no way to target another user.
/// </summary>
public sealed record UpdateProfileCommand(string DisplayName) : IRequest<Result<UserResponse>>;
