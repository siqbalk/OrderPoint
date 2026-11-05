using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Security;
using Identity.Application.Abstractions;
using Identity.Application.Features.EnableUser;
using Identity.Domain;
using NSubstitute;

namespace Identity.Tests;

public sealed class EnableUserHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly IIdentityDbContext _dbContext = Substitute.For<IIdentityDbContext>();
    private readonly TenantContext _tenantContext = new();
    private readonly Tenant _tenant = Tenant.Register("Acme", Now); // Free plan: 3 users

    public EnableUserHandlerTests()
    {
        _tenantContext.SetTenant(_tenant.Id);
        _dbContext.FindTenantAsync(_tenant.Id, Arg.Any<CancellationToken>()).Returns(_tenant);
    }

    private EnableUserHandler CreateHandler() => new(_dbContext, _tenantContext);

    private User GivenUser(bool acceptedInvitation = true)
    {
        var user = User.Invite(_tenant.Id, "mia@example.com", "Mia", TenantRole.Member, "hash", Now.AddDays(7), Now);
        if (acceptedInvitation)
            user.AcceptInvitation("pw-hash", Now);
        _dbContext.FindUserAsync(_tenant.Id, user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    [Fact]
    public async Task Enables_ADisabledUser_WhenASeatIsFree()
    {
        var user = GivenUser();
        user.Disable();
        _dbContext.CountUsersAsync(_tenant.Id, Arg.Any<CancellationToken>()).Returns(2);

        var result = await CreateHandler().Handle(new EnableUserCommand(user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Active", result.Value.Status);
        Assert.True(user.CanSignIn);
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_WhenThePlanHasNoFreeSeat()
    {
        var user = GivenUser();
        user.Disable();
        _dbContext.CountUsersAsync(_tenant.Id, Arg.Any<CancellationToken>()).Returns(3);

        var result = await CreateHandler().Handle(new EnableUserCommand(user.Id), CancellationToken.None);

        Assert.Equal("Identity.PlanLimitReached", result.Error.Code);
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_AUserWhoNeverAcceptedTheirInvitation()
    {
        var user = GivenUser(acceptedInvitation: false);
        user.Disable();

        var result = await CreateHandler().Handle(new EnableUserCommand(user.Id), CancellationToken.None);

        Assert.Equal("Identity.InvitationNeverAccepted", result.Error.Code);
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActiveUser_IsANoOp_WithoutCheckingSeats()
    {
        var user = GivenUser();

        var result = await CreateHandler().Handle(new EnableUserCommand(user.Id), CancellationToken.None);

        Assert.Equal("Active", result.Value.Status);
        await _dbContext.DidNotReceive().CountUsersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PendingInvitation_CannotBeEnabled()
    {
        var user = GivenUser(acceptedInvitation: false);

        var result = await CreateHandler().Handle(new EnableUserCommand(user.Id), CancellationToken.None);

        Assert.Equal("Identity.UserNotDisabled", result.Error.Code);
    }

    [Fact]
    public async Task Returns_NotFound_ForUnknownOrOtherTenantsUser()
    {
        var result = await CreateHandler().Handle(new EnableUserCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal("Identity.UserNotFound", result.Error.Code);
    }
}
