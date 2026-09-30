using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Outbox;
using Catalog.Contracts;
using Inventory.Contracts;
using NSubstitute;
using Sales.Application.Abstractions;
using Sales.Application.Features.PlaceOrder;
using Sales.Contracts.IntegrationEvents;
using Sales.Domain;

namespace Sales.Tests;

public sealed class PlaceOrderHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly ISalesDbContext _dbContext = Substitute.For<ISalesDbContext>();
    private readonly IProductCatalog _catalog = Substitute.For<IProductCatalog>();
    private readonly IInventoryAvailabilityChecker _availabilityChecker = Substitute.For<IInventoryAvailabilityChecker>();
    private readonly IOutboxWriter _outboxWriter = Substitute.For<IOutboxWriter>();
    private readonly TenantContext _tenant = new();

    public PlaceOrderHandlerTests()
    {
        _dbContext.Outbox.Returns(_outboxWriter);
        _tenant.SetTenant(TenantId);
        _catalog.GetBySkusAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, ProductSnapshot>
            {
                ["SKU-1"] = new(Guid.NewGuid(), "SKU-1", "Widget", 9.99m, IsActive: true),
                ["SKU-OLD"] = new(Guid.NewGuid(), "SKU-OLD", "Discontinued", 1m, IsActive: false),
            });
        _availabilityChecker.FindUnavailableAsync(Arg.Any<IReadOnlyCollection<StockRequirement>>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private PlaceOrderHandler CreateHandler() => new(_dbContext, _catalog, _availabilityChecker, _tenant, TimeProvider.System);

    [Fact]
    public async Task Handle_WhenStockAvailable_PersistsOrderAndEnqueuesIntegrationEvent()
    {
        var command = new PlaceOrderCommand("Ada Lovelace", "ada@example.com", [new PlaceOrderLine("sku-1", 3)]);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(29.97m, result.Value.Total);
        Assert.Equal(OrderStatus.Placed, result.Value.Status);
        _dbContext.Received(1).AddOrder(Arg.Is<Order>(o => o.Lines.Single().Sku == "SKU-1"));
        _outboxWriter.Received(1).Enqueue(Arg.Is<OrderPlacedIntegrationEvent>(e =>
            e.TenantId == TenantId && e.Lines.Single().Quantity == 3 && e.Total == 29.97m));
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UsesCatalogPrice_NotAClientSuppliedOne()
    {
        var result = await CreateHandler().Handle(
            new PlaceOrderCommand("Ada", "ada@example.com", [new PlaceOrderLine("SKU-1", 1)]), CancellationToken.None);

        Assert.Equal(9.99m, result.Value.Lines.Single().UnitPrice);
        Assert.Equal("Widget", result.Value.Lines.Single().ProductName);
    }

    [Fact]
    public async Task Handle_WhenStockUnavailable_ReturnsConflictAndDoesNotPersist()
    {
        _availabilityChecker.FindUnavailableAsync(Arg.Any<IReadOnlyCollection<StockRequirement>>(), Arg.Any<CancellationToken>())
            .Returns(["SKU-1"]);

        var result = await CreateHandler().Handle(
            new PlaceOrderCommand("Ada", "ada@example.com", [new PlaceOrderLine("SKU-1", 3)]), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Orders.InsufficientStock", result.Error.Code);
        _dbContext.DidNotReceive().AddOrder(Arg.Any<Order>());
        _outboxWriter.DidNotReceive().Enqueue(Arg.Any<BuildingBlocks.Messaging.IIntegrationEvent>());
    }

    [Theory]
    [InlineData("UNKNOWN")]
    [InlineData("SKU-OLD")]
    public async Task Handle_WhenProductUnknownOrInactive_Fails(string sku)
    {
        var result = await CreateHandler().Handle(
            new PlaceOrderCommand("Ada", "ada@example.com", [new PlaceOrderLine(sku, 1)]), CancellationToken.None);

        Assert.Equal("Orders.UnknownProduct", result.Error.Code);
        _dbContext.DidNotReceive().AddOrder(Arg.Any<Order>());
    }

    [Fact]
    public void Validator_RejectsDuplicateSkusAndEmptyOrders()
    {
        var validator = new PlaceOrderValidator();

        Assert.False(validator.Validate(new PlaceOrderCommand("Ada", "ada@example.com", [])).IsValid);
        Assert.False(validator.Validate(new PlaceOrderCommand("Ada", "ada@example.com",
            [new PlaceOrderLine("SKU-1", 1), new PlaceOrderLine("sku-1", 2)])).IsValid);
        Assert.False(validator.Validate(new PlaceOrderCommand("Ada", "not-an-email", [new PlaceOrderLine("SKU-1", 1)])).IsValid);
        Assert.True(validator.Validate(new PlaceOrderCommand("Ada", "ada@example.com", [new PlaceOrderLine("SKU-1", 1)])).IsValid);
    }
}
