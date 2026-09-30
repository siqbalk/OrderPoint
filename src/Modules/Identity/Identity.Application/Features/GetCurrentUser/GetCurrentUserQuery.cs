using BuildingBlocks.Results;
using MediatR;

namespace Identity.Application.Features.GetCurrentUser;

public sealed record GetCurrentUserQuery : IRequest<Result<CurrentUserResponse>>;

public sealed record CurrentUserResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    CurrentTenantResponse Tenant,
    IReadOnlyCollection<string> Permissions);

public sealed record CurrentTenantResponse(
    Guid Id,
    string Name,
    string Plan,
    int? MaxUsers,
    int? MaxProducts,
    int UserCount);
