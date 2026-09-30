using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Outbox;
using Catalog.Application.Abstractions;
using Catalog.Application.Features.CreateProduct;
using Catalog.Contracts.IntegrationEvents;
using Catalog.Domain;
using Identity.Contracts;
using NSubstitute;

namespace Catalog.Tests;

public sealed class CreateProductHandlerTests
{
    private readonly ICatalogDbContext _dbContext = Substitute.For<ICatalogDbContext>();
    private readonly ITenantPlanProvider _plans = Substitute.For<ITenantPlanProvider>();
    private readonly IOutboxWriter _outbox = Substitute.For<IOutboxWriter>();
    private readonly TenantContext _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    public CreateProductHandlerTests()
    {
        _dbContext.Outbox.Returns(_outbox);
        _tenant.SetTenant(_tenantId);
        _plans.GetPlanAsync(_tenantId, Arg.Any<CancellationToken>())
            .Returns(new TenantPlanInfo(_tenantId, "Free", IsActive: true, MaxUsers: 3, MaxProducts: 2));
    }

    private CreateProductHandler CreateHandler() => new(_dbContext, _tenant, _plans, TimeProvider.System);

    [Fact]
    public async Task Creates_NormalisesSku_AndPublishesProductCreated()
    {
        var result = await CreateHandler().Handle(new CreateProductCommand(" widget-1 ", "Widget", null, 12.499m), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("WIDGET-1", result.Value.Sku);
        Assert.Equal(12.50m, result.Value.Price);
        _outbox.Received(1).Enqueue(Arg.Is<ProductCreatedIntegrationEvent>(e => e.Sku == "WIDGET-1" && e.TenantId == _tenantId));
    }

    [Fact]
    public async Task Rejects_DuplicateSku()
    {
        _dbContext.SkuExistsAsync("WIDGET-1", Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().Handle(new CreateProductCommand("widget-1", "Widget", null, 1m), CancellationToken.None);

        Assert.Equal("Catalog.DuplicateSku", result.Error.Code);
    }

    [Fact]
    public async Task Enforces_PlanProductQuota()
    {
        _dbContext.CountProductsAsync(Arg.Any<CancellationToken>()).Returns(2);

        var result = await CreateHandler().Handle(new CreateProductCommand("X", "X", null, 1m), CancellationToken.None);

        Assert.Equal("Catalog.PlanLimitReached", result.Error.Code);
        _dbContext.DidNotReceive().AddProduct(Arg.Any<Product>());
    }

    [Fact]
    public void Validator_RejectsBadSkusAndPrices()
    {
        var validator = new CreateProductValidator();

        Assert.False(validator.Validate(new CreateProductCommand("has space", "X", null, 1m)).IsValid);
        Assert.False(validator.Validate(new CreateProductCommand("OK", "X", null, 0m)).IsValid);
        Assert.False(validator.Validate(new CreateProductCommand("OK", "X", null, 1.001m)).IsValid);
        Assert.True(validator.Validate(new CreateProductCommand("OK-1.b_2", "X", null, 19.99m)).IsValid);
    }
}
