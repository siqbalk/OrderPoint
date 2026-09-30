using BuildingBlocks.Results;
using MediatR;

namespace Identity.Application.Features.AcceptInvitation;

public sealed record AcceptInvitationCommand(string Token, string Password) : IRequest<Result<AuthTokenResponse>>;
