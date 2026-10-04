using BuildingBlocks.Results;
using Identity.Application.Features.ListUsers;
using MediatR;

namespace Identity.Application.Features.GetUser;

/// <summary>One user of the caller's tenant. Users of other tenants are reported as not found.</summary>
public sealed record GetUserQuery(Guid UserId) : IRequest<Result<UserResponse>>;
