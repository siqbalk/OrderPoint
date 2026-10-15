using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Security;
using Identity.Application.Abstractions;
using Identity.Application.Features.ListUsers;
using Identity.Domain;
using NSubstitute;

namespace Identity.Tests;

public sealed class ListUsersHandlerTests
{
    private readonly IIdentityDbContext _dbContext = Substitute.For<IIdentityDbContext>();
    private readonly TenantContext _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    public ListUsersHandlerTests() => _tenant.SetTenant(_tenantId);

    private ListUsersHandler CreateHandler() => new(_dbContext, _tenant);

    [Fact]
    public async Task PassesRoleAndStatus_ForTheCurrentTenant()
    {
        var now = DateTimeOffset.UtcNow;
        var invited = User.Invite(_tenantId, "mia@example.com", "Mia", TenantRole.Member, "hash", now.AddDays(7), now);
        _dbContext.ListUsersAsync(_tenantId, TenantRole.Member, UserStatus.Invited, Arg.Any<CancellationToken>())
            .Returns([invited]);

        var result = await CreateHandler().Handle(new ListUsersQuery(TenantRole.Member, UserStatus.Invited), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var user = Assert.Single(result.Value);
        Assert.Equal(invited.Id, user.Id);
        Assert.Equal("Invited", user.Status);
    }

    [Fact]
    public void Validator_RejectsUndefinedRoleOrStatus()
    {
        var validator = new ListUsersValidator();

        Assert.True(validator.Validate(new ListUsersQuery()).IsValid);
        Assert.True(validator.Validate(new ListUsersQuery(TenantRole.Admin, UserStatus.Active)).IsValid);
        Assert.False(validator.Validate(new ListUsersQuery((TenantRole)42)).IsValid);
        Assert.False(validator.Validate(new ListUsersQuery(Status: (UserStatus)42)).IsValid);
    }
}
