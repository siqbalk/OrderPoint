using BuildingBlocks.Results;
using BuildingBlocks.Security;
using Identity.Application.Abstractions;
using Identity.Domain;
using MediatR;

namespace Identity.Application.Features.Login;

public sealed class LoginHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer,
    IPermissionCatalog permissionCatalog,
    TimeProvider timeProvider)
    : IRequestHandler<LoginCommand, Result<AuthTokenResponse>>
{
    // Same response for "no such user" and "wrong password" so the endpoint
    // cannot be used to discover which email addresses have accounts.
    private static readonly Error InvalidCredentials =
        Error.Unauthorized("Identity.InvalidCredentials", "The email address or password is incorrect.");

    public async Task<Result<AuthTokenResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.FindUserByEmailAsync(User.NormalizeEmail(request.Email), cancellationToken);
        if (user is not { CanSignIn: true } || !passwordHasher.Verify(user.PasswordHash!, request.Password))
        {
            return Result.Failure<AuthTokenResponse>(InvalidCredentials);
        }

        var tenant = await dbContext.FindTenantAsync(user.TenantId, cancellationToken);
        if (tenant is not { IsActive: true })
        {
            return Result.Failure<AuthTokenResponse>(
                Error.Forbidden("Identity.TenantSuspended", "This organisation's account is suspended."));
        }

        user.RecordLogin(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        return AuthTokens.IssueFor(user, tokenIssuer, permissionCatalog);
    }
}
