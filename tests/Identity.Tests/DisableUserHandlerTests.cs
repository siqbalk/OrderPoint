using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Security;
using Identity.Application.Abstractions;
using Identity.Application.Features.DisableUser;
using Identity.Domain;
using NSubstitute;

namespace Identity.Tests;

public sealed class DisableUserHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly IIdentityDbContext _dbContext = Substitute.For<IIdentityDbContext>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly TenantContext _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    public DisableUserHandlerTests()
    {
        _tenant.SetTenant(_tenantId);
        _currentUser.UserId.Returns(Guid.NewGuid());
    }

    private DisableUserHandler CreateHandler() => new(_dbContext, _tenant, _currentUser);

    private User GivenInvitedMember()
    {
        var user = User.Invite(_tenantId, "mia@example.com", "Mia", TenantRole.Member, "hash", Now.AddDays(7), Now);
        _dbContext.FindUserAsync(_tenantId, user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    [Fact]
    public async Task Disables_AndRevokesPendingInvitation()
    {
        var user = GivenInvitedMember();

        var result = await CreateHandler().Handle(new DisableUserCommand(user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Disabled", result.Value.Status);
        Assert.Null(user.InvitationTokenHash);
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_DisablingYourself()
    {
        var user = GivenInvitedMember();
        _currentUser.UserId.Returns(user.Id);

        var result = await CreateHandler().Handle(new DisableUserCommand(user.Id), CancellationToken.None);

        Assert.Equal("Identity.CannotDisableSelf", result.Error.Code);
        Assert.Equal(UserStatus.Invited, user.Status);
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_DisablingTheOwner()
    {
        var owner = User.CreateOwner(_tenantId, "owner@example.com", "Owner", "hash", Now);
        _dbContext.FindUserAsync(_tenantId, owner.Id, Arg.Any<CancellationToken>()).Returns(owner);

        var result = await CreateHandler().Handle(new DisableUserCommand(owner.Id), CancellationToken.None);

        Assert.Equal("Identity.CannotDisableOwner", result.Error.Code);
        Assert.True(owner.CanSignIn);
    }

    [Fact]
    public async Task Returns_NotFound_ForUnknownOrOtherTenantsUser()
    {
        var result = await CreateHandler().Handle(new DisableUserCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal("Identity.UserNotFound", result.Error.Code);
    }
}
