using BuildingBlocks.Results;
using Identity.Application.Features.ListUsers;
using MediatR;

namespace Identity.Application.Features.EnableUser;

/// <summary>Restores a disabled user's access; they take a seat on the plan again.</summary>
public sealed record EnableUserCommand(Guid UserId) : IRequest<Result<UserResponse>>;
