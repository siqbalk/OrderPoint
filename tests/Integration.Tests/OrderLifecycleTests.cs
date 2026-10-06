using System.Net;
using System.Net.Http.Json;

namespace Integration.Tests;

/// <summary>
/// The whole cross-module saga end to end, over HTTP, against real PostgreSQL:
/// Catalog → (ProductCreated) → Inventory; Sales → (OrderPlaced) → Inventory
/// → (StockReserved) → Sales → (OrderConfirmed) → Notifications + Reporting;
/// cancel → (OrderCancelled) → Inventory releases, Reporting reverses.
/// No module references another module's internals — only *.Contracts.
/// </summary>
[Collection(PlatformCollection.Name)]
public sealed class OrderLifecycleTests(PlatformFixture fixture)
{
    [Fact]
    public async Task Order_IsConfirmed_ReportedAndNotified_ThenCancellationReleasesStock()
    {
        var tenant = await fixture.RegisterTenantAsync();
        var api = tenant.Client;

        // Catalog: create a product. Inventory opens a stock record for it via ProductCreated.
        var product = await (await api.PostAsJsonAsync("/api/catalog/products", new { Sku = "widget-1", Name = "Widget", Price = 12.50m }))
            .ReadAsync<ProductView>(HttpStatusCode.Created);
        Assert.Equal("WIDGET-1", product.Sku);

        await Api.EventuallyAsync(
            () => api.GetAsync("/api/inventory/stock/WIDGET-1"),
            r => r.StatusCode == HttpStatusCode.OK,
            "Inventory has created a stock record from ProductCreated");

        (await api.PostAsJsonAsync("/api/inventory/stock", new { Sku = "WIDGET-1", Quantity = 100 })).EnsureSuccessStatusCode();

        // Sales: the price comes from Catalog, not the request.
        var placed = await (await api.PostAsJsonAsync("/api/sales/orders", new
        {
            CustomerName = "Ada Lovelace",
            CustomerEmail = "ada@example.com",
            Lines = new[] { new { Sku = "widget-1", Quantity = 7 } },
        })).ReadAsync<OrderView>(HttpStatusCode.Created);
        Assert.Equal("Placed", placed.Status);
        Assert.Equal(87.50m, placed.Total);

        await Api.EventuallyAsync(
            () => api.GetAsync<OrderView>($"/api/sales/orders/{placed.Id}"),
            o => o.Status == "Confirmed",
            "Sales confirmed the order after Inventory reserved stock");

        var reserved = await api.GetAsync<StockView>("/api/inventory/stock/WIDGET-1");
        Assert.Equal(7, reserved.QuantityReserved);
        Assert.Equal(93, reserved.QuantityAvailable);

        await Api.EventuallyAsync(
            () => api.GetAsync<SalesReportView>("/api/reporting/sales"),
            r => r is { ConfirmedOrders: 1, Revenue: 87.50m, ItemsSold: 7 },
            "Reporting booked the confirmed order");

        var top = await api.GetAsync<TopOrdersView>("/api/reporting/sales/top-orders");
        Assert.Equal(new TopOrderView(placed.Id, 87.50m, 7), Assert.Single(top.Orders));

        // Cancel: the compensating path.
        var cancelled = await (await api.PostAsync($"/api/sales/orders/{placed.Id}/cancel", null)).ReadAsync<OrderView>();
        Assert.Equal("Cancelled", cancelled.Status);

        await Api.EventuallyAsync(
            () => api.GetAsync<StockView>("/api/inventory/stock/WIDGET-1"),
            s => s.QuantityReserved == 0 && s.QuantityAvailable == 100,
            "Inventory released the reservation");

        await Api.EventuallyAsync(
            () => api.GetAsync<SalesReportView>("/api/reporting/sales"),
            r => r is { CancelledOrders: 1, Revenue: 0m },
            "Reporting reversed the cancelled revenue");
        Assert.Empty((await api.GetAsync<TopOrdersView>("/api/reporting/sales/top-orders")).Orders);

        var notifications = await Api.EventuallyAsync(
            () => api.GetAsync<Paged<NotificationView>>("/api/notifications"),
            p => p.Items.Count >= 3,
            "welcome, confirmation and cancellation emails were sent");
        Assert.Contains(notifications.Items, n => n.Recipient == tenant.OwnerEmail && n.Subject.StartsWith("Welcome"));
        Assert.Contains(notifications.Items, n => n.Recipient == "ada@example.com" && n.Subject.EndsWith("confirmed"));
        Assert.Contains(notifications.Items, n => n.Recipient == "ada@example.com" && n.Subject.EndsWith("cancelled"));
    }

    [Fact]
    public async Task PlacingOrder_WithoutEnoughStock_IsRejectedUpFront()
    {
        var api = (await fixture.RegisterTenantAsync()).Client;
        (await api.PostAsJsonAsync("/api/catalog/products", new { Sku = "SCARCE", Name = "Scarce thing", Price = 5m })).EnsureSuccessStatusCode();
        (await api.PostAsJsonAsync("/api/inventory/stock", new { Sku = "SCARCE", Quantity = 2 })).EnsureSuccessStatusCode();

        var response = await api.PostAsJsonAsync("/api/sales/orders", new
        {
            CustomerName = "Grace Hopper",
            CustomerEmail = "grace@example.com",
            Lines = new[] { new { Sku = "SCARCE", Quantity = 3 } },
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Orders.InsufficientStock", await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task PlacingOrder_ForUnknownProduct_Fails()
    {
        var api = (await fixture.RegisterTenantAsync()).Client;

        var response = await api.PostAsJsonAsync("/api/sales/orders", new
        {
            CustomerName = "Grace Hopper",
            CustomerEmail = "grace@example.com",
            Lines = new[] { new { Sku = "NOPE", Quantity = 1 } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Orders.UnknownProduct", await response.ErrorCodeAsync());
    }
}
