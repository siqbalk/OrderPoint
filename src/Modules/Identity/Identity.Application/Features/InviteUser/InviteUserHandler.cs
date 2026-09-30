using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Results;
using Identity.Application.Abstractions;
using Identity.Contracts.IntegrationEvents;
using Identity.Domain;
using MediatR;

namespace Identity.Application.Features.InviteUser;

public sealed class InviteUserHandler(
    IIdentityDbContext dbContext,
    ITenantContext tenantContext,
    TimeProvider timeProvider)
    : IRequestHandler<InviteUserCommand, Result<InviteUserResponse>>
{
    public async Task<Result<InviteUserResponse>> Handle(InviteUserCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        var tenant = await dbContext.FindTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<InviteUserResponse>(Error.NotFound("Identity.TenantNotFound", "Tenant not found."));
        }

        var email = User.NormalizeEmail(request.Email);
        if (await dbContext.FindUserByEmailAsync(email, cancellationToken) is not null)
        {
            return Result.Failure<InviteUserResponse>(
                Error.Conflict("Identity.EmailTaken", "An account with this email address already exists."));
        }

        // Plan limits are a data-dependent business rule, so they are enforced
        // here in the handler rather than in an endpoint authorization policy.
        if (tenant.Limits.MaxUsers is { } maxUsers && await dbContext.CountUsersAsync(tenantId, cancellationToken) >= maxUsers)
        {
            return Result.Failure<InviteUserResponse>(Error.Forbidden(
                "Identity.PlanLimitReached",
                $"The {tenant.Plan} plan allows at most {maxUsers} users. Upgrade the plan to invite more."));
        }

        var now = timeProvider.GetUtcNow();
        var expiresOn = now.Add(InvitationTokens.Lifetime);
        var (token, tokenHash) = InvitationTokens.Generate();
        var user = User.Invite(tenantId, email, request.DisplayName, request.Role, tokenHash, expiresOn, now);

        dbContext.AddUser(user);
        dbContext.Outbox.Enqueue(new UserInvitedIntegrationEvent(
            Guid.NewGuid(), now, tenantId, tenant.Name, user.Id, user.Email, user.DisplayName, user.Role.ToString(), token, expiresOn));

        await dbContext.SaveChangesAsync(cancellationToken);

        return new InviteUserResponse(user.Id, user.Email, user.Role.ToString(), expiresOn);
    }
}
