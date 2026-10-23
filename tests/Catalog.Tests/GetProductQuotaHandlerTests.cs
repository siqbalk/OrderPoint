using BuildingBlocks.MultiTenancy;
using Catalog.Application.Abstractions;
using Catalog.Application.Features.GetProductQuota;
using Identity.Contracts;
using NSubstitute;

namespace Catalog.Tests;

public sealed class GetProductQuotaHandlerTests
{
    private readonly ICatalogDbContext _dbContext = Substitute.For<ICatalogDbContext>();
    private readonly ITenantPlanProvider _plans = Substitute.For<ITenantPlanProvider>();
    private readonly TenantContext _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    public GetProductQuotaHandlerTests() => _tenant.SetTenant(_tenantId);

    private GetProductQuotaHandler CreateHandler() => new(_dbContext, _tenant, _plans);

    private void GivenPlan(int? maxProducts)
        => _plans.GetPlanAsync(_tenantId, Arg.Any<CancellationToken>())
            .Returns(new TenantPlanInfo(_tenantId, "Free", IsActive: true, MaxUsers: 3, MaxProducts: maxProducts));

    [Fact]
    public async Task Returns_UsedAndRemaining_AgainstThePlanLimit()
    {
        GivenPlan(maxProducts: 25);
        _dbContext.CountProductsAsync(Arg.Any<CancellationToken>()).Returns(7);

        var result = await CreateHandler().Handle(new GetProductQuotaQuery(), CancellationToken.None);

        Assert.Equal(new ProductQuotaResponse("Free", 7, 25, 18), result.Value);
    }

    [Fact]
    public async Task Remaining_NeverGoesNegative_WhenOverTheLimit()
    {
        // e.g. after downgrading to a plan with a lower limit.
        GivenPlan(maxProducts: 25);
        _dbContext.CountProductsAsync(Arg.Any<CancellationToken>()).Returns(30);

        var result = await CreateHandler().Handle(new GetProductQuotaQuery(), CancellationToken.None);

        Assert.Equal(0, result.Value.Remaining);
    }

    [Fact]
    public async Task UnlimitedPlan_HasNoMaxOrRemaining()
    {
        GivenPlan(maxProducts: null);
        _dbContext.CountProductsAsync(Arg.Any<CancellationToken>()).Returns(1_000);

        var result = await CreateHandler().Handle(new GetProductQuotaQuery(), CancellationToken.None);

        Assert.Equal(new ProductQuotaResponse("Free", 1_000, null, null), result.Value);
    }
}
