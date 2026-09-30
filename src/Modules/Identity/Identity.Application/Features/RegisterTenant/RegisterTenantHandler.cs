using BuildingBlocks.Results;
using BuildingBlocks.Security;
using Identity.Application.Abstractions;
using Identity.Contracts.IntegrationEvents;
using Identity.Domain;
using MediatR;

namespace Identity.Application.Features.RegisterTenant;

public sealed class RegisterTenantHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer,
    IPermissionCatalog permissionCatalog,
    TimeProvider timeProvider)
    : IRequestHandler<RegisterTenantCommand, Result<AuthTokenResponse>>
{
    public async Task<Result<AuthTokenResponse>> Handle(RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(request.OwnerEmail);
        if (await dbContext.FindUserByEmailAsync(email, cancellationToken) is not null)
        {
            return Result.Failure<AuthTokenResponse>(
                Error.Conflict("Identity.EmailTaken", "An account with this email address already exists."));
        }

        var now = timeProvider.GetUtcNow();
        var tenant = Tenant.Register(request.TenantName, now);
        var owner = User.CreateOwner(tenant.Id, email, request.OwnerDisplayName, passwordHasher.Hash(request.Password), now);

        dbContext.AddTenant(tenant);
        dbContext.AddUser(owner);
        dbContext.Outbox.Enqueue(new TenantRegisteredIntegrationEvent(
            Guid.NewGuid(), now, tenant.Id, tenant.Name, owner.Id, owner.Email, owner.DisplayName));

        await dbContext.SaveChangesAsync(cancellationToken);

        return AuthTokens.IssueFor(owner, tokenIssuer, permissionCatalog);
    }
}
