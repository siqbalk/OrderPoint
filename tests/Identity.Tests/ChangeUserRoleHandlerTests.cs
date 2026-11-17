using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Security;
using Identity.Application.Abstractions;
using Identity.Application.Features.ChangeUserRole;
using Identity.Domain;
using NSubstitute;

namespace Identity.Tests;

public sealed class ChangeUserRoleHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly IIdentityDbContext _dbContext = Substitute.For<IIdentityDbContext>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly TenantContext _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    public ChangeUserRoleHandlerTests()
    {
        _tenant.SetTenant(_tenantId);
        _currentUser.UserId.Returns(Guid.NewGuid());
    }

    private ChangeUserRoleHandler CreateHandler() => new(_dbContext, _tenant, _currentUser);

    private User Given(User user)
    {
        _dbContext.FindUserAsync(_tenantId, user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    private User GivenMember()
        => Given(User.Invite(_tenantId, "mia@example.com", "Mia", TenantRole.Member, "hash", Now.AddDays(7), Now));

    [Fact]
    public async Task Promotes_AMemberToAdmin()
    {
        var user = GivenMember();

        var result = await CreateHandler().Handle(new ChangeUserRoleCommand(user.Id, TenantRole.Admin), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Admin", result.Value.Role);
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_ChangingYourOwnRole()
    {
        var user = GivenMember();
        _currentUser.UserId.Returns(user.Id);

        var result = await CreateHandler().Handle(new ChangeUserRoleCommand(user.Id, TenantRole.Admin), CancellationToken.None);

        Assert.Equal("Identity.CannotChangeOwnRole", result.Error.Code);
        Assert.Equal(TenantRole.Member, user.Role);
    }

    [Fact]
    public async Task Rejects_ChangingTheOwnersRole()
    {
        var owner = Given(User.CreateOwner(_tenantId, "owner@example.com", "Owner", "hash", Now));

        var result = await CreateHandler().Handle(new ChangeUserRoleCommand(owner.Id, TenantRole.Member), CancellationToken.None);

        Assert.Equal("Identity.CannotChangeOwnerRole", result.Error.Code);
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Domain_RefusesToMakeAnotherOwner()
    {
        var user = User.Invite(_tenantId, "mia@example.com", "Mia", TenantRole.Member, "hash", Now.AddDays(7), Now);

        Assert.Equal("Identity.InvalidRole", user.ChangeRole(TenantRole.Owner).Error.Code);
        Assert.Equal(TenantRole.Member, user.Role);
    }

    [Fact]
    public async Task Returns_NotFound_ForUnknownOrOtherTenantsUser()
    {
        var result = await CreateHandler().Handle(new ChangeUserRoleCommand(Guid.NewGuid(), TenantRole.Admin), CancellationToken.None);

        Assert.Equal("Identity.UserNotFound", result.Error.Code);
    }

    [Fact]
    public void Validator_RejectsOwnerAndUndefinedRoles()
    {
        var validator = new ChangeUserRoleValidator();

        Assert.True(validator.Validate(new ChangeUserRoleCommand(Guid.NewGuid(), TenantRole.Member)).IsValid);
        Assert.False(validator.Validate(new ChangeUserRoleCommand(Guid.NewGuid(), TenantRole.Owner)).IsValid);
        Assert.False(validator.Validate(new ChangeUserRoleCommand(Guid.NewGuid(), (TenantRole)42)).IsValid);
    }
}
