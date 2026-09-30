using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Outbox;
using BuildingBlocks.Security;
using Identity.Application.Abstractions;
using Identity.Application.Features.InviteUser;
using Identity.Application.Features.Login;
using Identity.Contracts.IntegrationEvents;
using Identity.Domain;
using Identity.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;

namespace Identity.Tests;

public sealed class IdentityTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly IIdentityDbContext _dbContext = Substitute.For<IIdentityDbContext>();
    private readonly IOutboxWriter _outbox = Substitute.For<IOutboxWriter>();
    private readonly PasswordHasher _hasher = new();

    public IdentityTests() => _dbContext.Outbox.Returns(_outbox);

    [Fact]
    public void PasswordHasher_VerifiesOnlyTheOriginalPassword()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.True(_hasher.Verify(hash, "correct horse battery staple"));
        Assert.False(_hasher.Verify(hash, "Correct horse battery staple"));
        Assert.DoesNotContain("correct", hash);
    }

    [Fact]
    public void PermissionCatalog_GrantsRolesCumulatively()
    {
        var catalog = new PermissionCatalog(
        [
            new("m:thing:read", "", TenantRole.Member),
            new("m:thing:write", "", TenantRole.Admin),
            new("m:tenant:manage", "", TenantRole.Owner),
        ]);

        Assert.Equal(["m:thing:read"], catalog.ForRole(TenantRole.Member));
        Assert.Equal(2, catalog.ForRole(TenantRole.Admin).Count);
        Assert.Equal(3, catalog.ForRole(TenantRole.Owner).Count);
    }

    [Fact]
    public void JwtTokenIssuer_EmbedsTenantRoleAndPermissions()
    {
        var settings = Options.Create(new JwtSettings
        {
            Issuer = "issuer", Audience = "audience", SigningKey = new string('k', 32), AccessTokenLifetimeMinutes = 5,
        });
        var tenant = Tenant.Register("Acme", Now);
        var user = User.CreateOwner(tenant.Id, "Owner@Example.com", "Owner", "hash", Now);

        var token = new JwtTokenIssuer(settings, TimeProvider.System).Issue(user, ["a:b:c", "d:e:f"]);
        var jwt = new JsonWebToken(token.Token);

        Assert.Equal(tenant.Id.ToString(), jwt.GetClaim(ClaimNames.TenantId).Value);
        Assert.Equal("owner@example.com", jwt.GetClaim(ClaimNames.Email).Value);
        Assert.Equal("Owner", jwt.GetClaim(ClaimNames.Role).Value);
        Assert.Equal(["a:b:c", "d:e:f"], jwt.Claims.Where(c => c.Type == ClaimNames.Permission).Select(c => c.Value));
    }

    [Fact]
    public async Task Login_ReturnsSameError_ForUnknownUserAndWrongPassword()
    {
        var tenant = Tenant.Register("Acme", Now);
        var user = User.CreateOwner(tenant.Id, "owner@example.com", "Owner", _hasher.Hash("right password"), Now);
        _dbContext.FindUserByEmailAsync("owner@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _dbContext.FindTenantAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        var handler = new LoginHandler(_dbContext, _hasher, Substitute.For<ITokenIssuer>(), new PermissionCatalog([]), TimeProvider.System);

        var wrongPassword = await handler.Handle(new LoginCommand("owner@example.com", "wrong password"), CancellationToken.None);
        var unknownUser = await handler.Handle(new LoginCommand("nobody@example.com", "whatever"), CancellationToken.None);

        Assert.Equal("Identity.InvalidCredentials", wrongPassword.Error.Code);
        Assert.Equal(wrongPassword.Error, unknownUser.Error);
    }

    [Fact]
    public async Task InviteUser_EnforcesPlanUserLimit()
    {
        var tenant = Tenant.Register("Acme", Now); // Free plan: 3 users
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenant.Id);
        _dbContext.FindTenantAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);
        _dbContext.CountUsersAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(3);

        var result = await new InviteUserHandler(_dbContext, tenantContext, TimeProvider.System)
            .Handle(new InviteUserCommand("new@example.com", "New", TenantRole.Member), CancellationToken.None);

        Assert.Equal("Identity.PlanLimitReached", result.Error.Code);
        _dbContext.DidNotReceive().AddUser(Arg.Any<User>());
    }

    [Fact]
    public async Task InviteUser_StoresOnlyTheTokenHash_AndPublishesTheRawToken()
    {
        var tenant = Tenant.Register("Acme", Now);
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenant.Id);
        _dbContext.FindTenantAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        User? invited = null;
        _dbContext.When(d => d.AddUser(Arg.Any<User>())).Do(c => invited = c.Arg<User>());
        UserInvitedIntegrationEvent? published = null;
        _outbox.When(o => o.Enqueue(Arg.Any<UserInvitedIntegrationEvent>())).Do(c => published = c.Arg<UserInvitedIntegrationEvent>());

        var result = await new InviteUserHandler(_dbContext, tenantContext, TimeProvider.System)
            .Handle(new InviteUserCommand("new@example.com", "New", TenantRole.Admin), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(invited);
        Assert.NotNull(published);
        Assert.Equal(InvitationTokens.Hash(published.InvitationToken), invited.InvitationTokenHash);
        Assert.NotEqual(published.InvitationToken, invited.InvitationTokenHash);
        Assert.False(invited.CanSignIn);
    }

    [Fact]
    public void AcceptInvitation_FailsOnceExpired()
    {
        var user = User.Invite(Guid.NewGuid(), "a@example.com", "A", TenantRole.Member, "hash", expiresOnUtc: Now, now: Now.AddDays(-7));

        Assert.Equal("Identity.InvitationExpired", user.AcceptInvitation("pw-hash", Now.AddMinutes(1)).Error.Code);
        Assert.True(user.AcceptInvitation("pw-hash", Now.AddMinutes(-1)).IsSuccess);
        Assert.True(user.CanSignIn);
    }
}
