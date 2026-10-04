using BuildingBlocks.MultiTenancy;
using Identity.Application.Abstractions;
using Identity.Application.Features.GetUser;
using Identity.Domain;
using NSubstitute;

namespace Identity.Tests;

public sealed class GetUserHandlerTests
{
    private readonly IIdentityDbContext _dbContext = Substitute.For<IIdentityDbContext>();
    private readonly TenantContext _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    public GetUserHandlerTests() => _tenant.SetTenant(_tenantId);

    private GetUserHandler CreateHandler() => new(_dbContext, _tenant);

    [Fact]
    public async Task Returns_User_FromCurrentTenant()
    {
        var user = User.CreateOwner(_tenantId, "owner@example.com", "Owner", "hash", DateTimeOffset.UtcNow);
        _dbContext.FindUserAsync(_tenantId, user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateHandler().Handle(new GetUserQuery(user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value.Id);
        Assert.Equal("owner@example.com", result.Value.Email);
        Assert.Equal("Owner", result.Value.Role);
    }

    [Fact]
    public async Task Returns_NotFound_ForUserOfAnotherTenant()
    {
        var otherTenantUser = User.CreateOwner(Guid.NewGuid(), "other@example.com", "Other", "hash", DateTimeOffset.UtcNow);
        _dbContext.FindUserAsync(otherTenantUser.TenantId, otherTenantUser.Id, Arg.Any<CancellationToken>()).Returns(otherTenantUser);

        var result = await CreateHandler().Handle(new GetUserQuery(otherTenantUser.Id), CancellationToken.None);

        Assert.Equal("Identity.UserNotFound", result.Error.Code);
        await _dbContext.Received(1).FindUserAsync(_tenantId, otherTenantUser.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_RejectsEmptyUserId()
    {
        var validator = new GetUserValidator();

        Assert.False(validator.Validate(new GetUserQuery(Guid.Empty)).IsValid);
        Assert.True(validator.Validate(new GetUserQuery(Guid.NewGuid())).IsValid);
    }
}
