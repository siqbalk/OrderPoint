using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Security;
using Identity.Application.Abstractions;
using Identity.Application.Features.UpdateProfile;
using Identity.Domain;
using NSubstitute;

namespace Identity.Tests;

public sealed class UpdateProfileHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly IIdentityDbContext _dbContext = Substitute.For<IIdentityDbContext>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly TenantContext _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    public UpdateProfileHandlerTests() => _tenant.SetTenant(_tenantId);

    private UpdateProfileHandler CreateHandler() => new(_dbContext, _tenant, _currentUser);

    private User GivenSignedIn()
    {
        var user = User.CreateOwner(_tenantId, "olive@example.com", "Olive", "hash", Now);
        _currentUser.UserId.Returns(user.Id);
        _dbContext.FindUserAsync(_tenantId, user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    [Fact]
    public async Task Renames_TheSignedInUser_TrimmingWhitespace()
    {
        var user = GivenSignedIn();

        var result = await CreateHandler().Handle(new UpdateProfileCommand("  Olive Owner  "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Olive Owner", result.Value.DisplayName);
        Assert.Equal("Olive Owner", user.DisplayName);
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_NotFound_WhenTheSignedInUserNoLongerExists()
    {
        _currentUser.UserId.Returns(Guid.NewGuid());

        var result = await CreateHandler().Handle(new UpdateProfileCommand("Anyone"), CancellationToken.None);

        Assert.Equal("Identity.UserNotFound", result.Error.Code);
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Domain_RejectsABlankName()
    {
        var user = User.CreateOwner(_tenantId, "olive@example.com", "Olive", "hash", Now);

        Assert.Equal("Identity.DisplayNameRequired", user.Rename("   ").Error.Code);
        Assert.Equal("Olive", user.DisplayName);
    }

    [Fact]
    public void Validator_RequiresANameOfAtMost100Characters()
    {
        var validator = new UpdateProfileValidator();

        Assert.True(validator.Validate(new UpdateProfileCommand("Olive")).IsValid);
        Assert.False(validator.Validate(new UpdateProfileCommand("")).IsValid);
        Assert.False(validator.Validate(new UpdateProfileCommand(new string('x', 101))).IsValid);
    }
}
