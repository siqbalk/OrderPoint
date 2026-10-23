using System.Net.Http.Json;

namespace Integration.Tests;

[Collection(PlatformCollection.Name)]
public sealed class CatalogQuotaTests(PlatformFixture fixture)
{
    [Fact]
    public async Task ProductQuota_TracksUsage_AndFollowsPlanChanges()
    {
        var api = (await fixture.RegisterTenantAsync()).Client;
        Assert.Equal(new ProductQuotaView("Free", 0, 25, 25), await api.GetAsync<ProductQuotaView>("/api/catalog/products/quota"));

        (await api.PostAsJsonAsync("/api/catalog/products", new { Sku = "Q-1", Name = "One", Price = 1m })).EnsureSuccessStatusCode();
        (await api.PostAsJsonAsync("/api/catalog/products", new { Sku = "Q-2", Name = "Two", Price = 2m })).EnsureSuccessStatusCode();
        Assert.Equal(new ProductQuotaView("Free", 2, 25, 23), await api.GetAsync<ProductQuotaView>("/api/catalog/products/quota"));

        (await api.PutAsJsonAsync("/api/identity/tenant/plan", new { Plan = "Enterprise" })).EnsureSuccessStatusCode();
        Assert.Equal(new ProductQuotaView("Enterprise", 2, null, null), await api.GetAsync<ProductQuotaView>("/api/catalog/products/quota"));
    }
}
