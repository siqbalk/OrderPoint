using BuildingBlocks.Results;
using MediatR;

namespace Identity.Application.Features.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<AuthTokenResponse>>;
