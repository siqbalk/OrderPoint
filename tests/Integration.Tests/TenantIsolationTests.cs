using System.Net;
using System.Net.Http.Json;

namespace Integration.Tests;

/// <summary>
/// Tenant data isolation is enforced by the DbContext query filters, not by
/// handler code. These tests prove one tenant cannot see or use another's data.
/// </summary>
[Collection(PlatformCollection.Name)]
public sealed class TenantIsolationTests(PlatformFixture fixture)
{
    [Fact]
    public async Task Tenant_CannotReadOrUse_AnotherTenantsData()
    {
        var acme = (await fixture.RegisterTenantAsync("Acme")).Client;
        var globex = (await fixture.RegisterTenantAsync("Globex")).Client;

        var acmeProduct = await (await acme.PostAsJsonAsync("/api/catalog/products", new { Sku = "SHARED-SKU", Name = "Acme anvil", Price = 99m }))
            .ReadAsync<ProductView>(HttpStatusCode.Created);
        (await acme.PostAsJsonAsync("/api/inventory/stock", new { Sku = "SHARED-SKU", Quantity = 10 })).EnsureSuccessStatusCode();
        var acmeOrder = await (await acme.PostAsJsonAsync("/api/sales/orders", new
        {
            CustomerName = "Wile E. Coyote",
            CustomerEmail = "wile@example.com",
            Lines = new[] { new { Sku = "SHARED-SKU", Quantity = 1 } },
        })).ReadAsync<OrderView>(HttpStatusCode.Created);

        // Globex sees none of it.
        Assert.Equal(HttpStatusCode.NotFound, (await globex.GetAsync($"/api/catalog/products/{acmeProduct.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await globex.GetAsync($"/api/sales/orders/{acmeOrder.Id}")).StatusCode);
        Assert.Equal(0, (await globex.GetAsync<Paged<ProductView>>("/api/catalog/products")).TotalCount);
        Assert.Equal(0, (await globex.GetAsync<Paged<OrderView>>("/api/sales/orders")).TotalCount);
        Assert.Equal(HttpStatusCode.NotFound, (await globex.PostAsync($"/api/sales/orders/{acmeOrder.Id}/cancel", null)).StatusCode);

        // …cannot order against Acme's catalog…
        var crossTenantOrder = await globex.PostAsJsonAsync("/api/sales/orders", new
        {
            CustomerName = "Hank Scorpio",
            CustomerEmail = "hank@example.com",
            Lines = new[] { new { Sku = "SHARED-SKU", Quantity = 1 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, crossTenantOrder.StatusCode);

        // …and SKUs are unique per tenant, not globally.
        (await globex.PostAsJsonAsync("/api/catalog/products", new { Sku = "SHARED-SKU", Name = "Globex doomsday device", Price = 1m }))
            .EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Tenant_CanReadOwnNotificationById_ButNotAnotherTenants()
    {
        var acme = await fixture.RegisterTenantAsync("Acme");
        var globex = (await fixture.RegisterTenantAsync("Globex")).Client;

        var notifications = await Api.EventuallyAsync(
            () => acme.Client.GetAsync<Paged<NotificationView>>("/api/notifications"),
            page => page.Items.Any(n => n.Recipient == acme.OwnerEmail),
            "the welcome email is sent asynchronously via the outbox");
        var welcome = notifications.Items.First(n => n.Recipient == acme.OwnerEmail);

        var fetched = await (await acme.Client.GetAsync($"/api/notifications/{welcome.Id}")).ReadAsync<NotificationView>();
        Assert.Equal(welcome, fetched);

        Assert.Equal(HttpStatusCode.NotFound, (await globex.GetAsync($"/api/notifications/{welcome.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await acme.Client.GetAsync($"/api/notifications/{Guid.NewGuid()}")).StatusCode);
    }
}
