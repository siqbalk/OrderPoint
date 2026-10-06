using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Integration.Tests;

/// <summary>Thin typed helpers over HttpClient so the tests read as user journeys.</summary>
internal static class Api
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Signs up a brand-new tenant and returns a client authenticated as its owner.</summary>
    public static async Task<TenantSession> RegisterTenantAsync(this PlatformFixture fixture, string? name = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"owner-{suffix}@example.com";
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/identity/tenants/register", new
        {
            TenantName = name ?? $"Tenant {suffix}",
            OwnerEmail = email,
            OwnerDisplayName = "Owner",
            Password = "correct horse battery staple",
        });
        var token = await response.ReadAsync<AuthToken>(HttpStatusCode.Created);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return new TenantSession(client, token.TenantId, email);
    }

    public static HttpClient WithToken(this PlatformFixture fixture, string accessToken)
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {(int)expected} but got {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, Json)!;
    }

    public static async Task<T> GetAsync<T>(this HttpClient client, string url)
        => await (await client.GetAsync(url)).ReadAsync<T>();

    public static async Task<string> ErrorCodeAsync(this HttpResponseMessage response)
    {
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.TryGetProperty("code", out var code) ? code.GetString()! : string.Empty;
    }

    /// <summary>
    /// Polls until <paramref name="condition"/> holds. Needed wherever a result
    /// depends on the outbox delivering an integration event asynchronously.
    /// </summary>
    public static async Task<T> EventuallyAsync<T>(Func<Task<T>> probe, Func<T, bool> condition, string because, int timeoutSeconds = 15)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        T last;
        do
        {
            last = await probe();
            if (condition(last))
            {
                return last;
            }

            await Task.Delay(200);
        } while (DateTime.UtcNow < deadline);

        Assert.Fail($"Timed out waiting until {because}. Last value: {JsonSerializer.Serialize(last, Json)}");
        return last;
    }
}

internal sealed record TenantSession(HttpClient Client, Guid TenantId, string OwnerEmail);

internal sealed record AuthToken(string AccessToken, string TokenType, DateTimeOffset ExpiresOnUtc, Guid TenantId, Guid UserId);

internal sealed record Paged<T>(List<T> Items, int Page, int PageSize, int TotalCount);

internal sealed record ProductView(Guid Id, string Sku, string Name, decimal Price, bool IsActive);

internal sealed record StockView(Guid Id, string Sku, int QuantityOnHand, int QuantityReserved, int QuantityAvailable);

internal sealed record OrderView(Guid Id, string Status, decimal Total, string? RejectionReason, List<OrderLineView> Lines);

internal sealed record OrderLineView(string Sku, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);

internal sealed record SalesReportView(decimal Revenue, int ConfirmedOrders, int CancelledOrders, int ItemsSold);

internal sealed record TopOrdersView(IReadOnlyList<TopOrderView> Orders);

internal sealed record TopOrderView(Guid OrderId, decimal Total, int ItemCount);

internal sealed record NotificationView(Guid Id, string Recipient, string Subject, string Body);

internal sealed record MeView(Guid UserId, string Email, string Role, MeTenantView Tenant, List<string> Permissions);

internal sealed record MeTenantView(Guid Id, string Name, string Plan, int? MaxUsers, int? MaxProducts, int UserCount);
