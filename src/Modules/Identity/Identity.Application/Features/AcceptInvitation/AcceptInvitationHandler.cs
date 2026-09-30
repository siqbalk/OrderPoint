using BuildingBlocks.Results;
using BuildingBlocks.Security;
using Identity.Application.Abstractions;
using MediatR;

namespace Identity.Application.Features.AcceptInvitation;

public sealed class AcceptInvitationHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer,
    IPermissionCatalog permissionCatalog,
    TimeProvider timeProvider)
    : IRequestHandler<AcceptInvitationCommand, Result<AuthTokenResponse>>
{
    public async Task<Result<AuthTokenResponse>> Handle(AcceptInvitationCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.FindUserByInvitationTokenHashAsync(InvitationTokens.Hash(request.Token), cancellationToken);
        if (user is null)
        {
            return Result.Failure<AuthTokenResponse>(
                Error.NotFound("Identity.InvitationNotFound", "This invitation link is invalid or has already been used."));
        }

        var accepted = user.AcceptInvitation(passwordHasher.Hash(request.Password), timeProvider.GetUtcNow());
        if (accepted.IsFailure)
        {
            return Result.Failure<AuthTokenResponse>(accepted.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return AuthTokens.IssueFor(user, tokenIssuer, permissionCatalog);
    }
}
