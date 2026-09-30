using BuildingBlocks.Results;
using BuildingBlocks.Security;
using MediatR;

namespace Identity.Application.Features.InviteUser;

public sealed record InviteUserCommand(string Email, string DisplayName, TenantRole Role) : IRequest<Result<InviteUserResponse>>;

public sealed record InviteUserResponse(Guid UserId, string Email, string Role, DateTimeOffset ExpiresOnUtc);
