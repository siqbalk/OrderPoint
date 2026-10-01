using Catalog.Application.Abstractions;
using Catalog.Application.Features.GetProductBySku;
using Catalog.Domain;
using NSubstitute;

namespace Catalog.Tests;

public sealed class GetProductBySkuHandlerTests
{
    private readonly ICatalogDbContext _dbContext = Substitute.For<ICatalogDbContext>();

    private GetProductBySkuHandler CreateHandler() => new(_dbContext);

    [Fact]
    public async Task Returns_Product_LookingUpByNormalisedSku()
    {
        var product = Product.Create("WIDGET-1", "Widget", null, 9.99m, DateTimeOffset.UtcNow);
        _dbContext.FindBySkuAsync("WIDGET-1", Arg.Any<CancellationToken>()).Returns(product);

        var result = await CreateHandler().Handle(new GetProductBySkuQuery(" widget-1 "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(product.Id, result.Value.Id);
        Assert.Equal("WIDGET-1", result.Value.Sku);
    }

    [Fact]
    public async Task Returns_NotFound_WhenSkuUnknown()
    {
        var result = await CreateHandler().Handle(new GetProductBySkuQuery("missing"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Catalog.ProductNotFound", result.Error.Code);
    }

    [Fact]
    public void Validator_RejectsEmptyAndOverlongSkus()
    {
        var validator = new GetProductBySkuValidator();

        Assert.False(validator.Validate(new GetProductBySkuQuery("")).IsValid);
        Assert.False(validator.Validate(new GetProductBySkuQuery(new string('A', 51))).IsValid);
        Assert.True(validator.Validate(new GetProductBySkuQuery("WIDGET-1")).IsValid);
    }
}
