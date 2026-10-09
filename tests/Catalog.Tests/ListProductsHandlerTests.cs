using BuildingBlocks.Pagination;
using Catalog.Application.Abstractions;
using Catalog.Application.Features.ListProducts;
using Catalog.Domain;
using NSubstitute;

namespace Catalog.Tests;

public sealed class ListProductsHandlerTests
{
    private readonly ICatalogDbContext _dbContext = Substitute.For<ICatalogDbContext>();

    private ListProductsHandler CreateHandler() => new(_dbContext);

    [Fact]
    public async Task PassesPriceRange_ToTheQuery()
    {
        var product = Product.Create("WIDGET-1", "Widget", null, 15m, DateTimeOffset.UtcNow);
        _dbContext.ListAsync("wid", false, 10m, 20m, 1, 25, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Product>([product], 1, 25, 1));

        var result = await CreateHandler().Handle(new ListProductsQuery("wid", false, 10m, 20m, 1, 25), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(product.Id, Assert.Single(result.Value.Items).Id);
    }

    [Fact]
    public void Validator_RejectsNegativeBounds_AndMaxBelowMin()
    {
        var validator = new ListProductsValidator();

        Assert.True(validator.Validate(new ListProductsQuery(null, false, 10m, 10m)).IsValid);
        Assert.True(validator.Validate(new ListProductsQuery(null, false, null, 5m)).IsValid);
        Assert.False(validator.Validate(new ListProductsQuery(null, false, -1m, null)).IsValid);
        Assert.False(validator.Validate(new ListProductsQuery(null, false, null, -1m)).IsValid);
        Assert.False(validator.Validate(new ListProductsQuery(null, false, 20m, 10m)).IsValid);
    }
}
