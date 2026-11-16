using BuildingBlocks.Results;
using BuildingBlocks.Security;
using Identity.Application.Features.ListUsers;
using MediatR;

namespace Identity.Application.Features.ChangeUserRole;

/// <summary>
/// Makes a user an Admin or a Member. Roles travel in the access token, so the
/// change takes effect the next time the user signs in.
/// </summary>
public sealed record ChangeUserRoleCommand(Guid UserId, TenantRole Role) : IRequest<Result<UserResponse>>;
