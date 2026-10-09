using System.Net;
using System.Net.Http.Json;

namespace Integration.Tests;

[Collection(PlatformCollection.Name)]
public sealed class CatalogSearchTests(PlatformFixture fixture)
{
    [Fact]
    public async Task ListProducts_FiltersByInclusivePriceRange()
    {
        var api = (await fixture.RegisterTenantAsync()).Client;
        foreach (var (sku, price) in new[] { ("CHEAP", 5m), ("MID", 10m), ("PRICEY", 20m) })
        {
            (await api.PostAsJsonAsync("/api/catalog/products", new { Sku = sku, Name = sku, Price = price })).EnsureSuccessStatusCode();
        }

        var between = await api.GetAsync<Paged<ProductView>>("/api/catalog/products?minPrice=10&maxPrice=20");
        Assert.Equal(["MID", "PRICEY"], between.Items.Select(p => p.Sku).Order());
        Assert.Equal(2, between.TotalCount);

        var atMost = await api.GetAsync<Paged<ProductView>>("/api/catalog/products?maxPrice=9.99");
        Assert.Equal("CHEAP", Assert.Single(atMost.Items).Sku);

        var inverted = await api.GetAsync("/api/catalog/products?minPrice=20&maxPrice=10");
        Assert.Equal(HttpStatusCode.BadRequest, inverted.StatusCode);
    }
}
