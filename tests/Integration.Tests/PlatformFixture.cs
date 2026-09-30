using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Integration.Tests;

/// <summary>
/// Hosts the real composition root (all six modules, the outbox worker, auth,
/// rate limiting) in-process against a throwaway PostgreSQL container. The
/// schema is created by running the shipped scripts/migrations/*.sql files —
/// exactly what a deployment does — rather than by calling Migrate().
/// </summary>
public sealed class PlatformFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestSigningKey = "integration-test-signing-key-at-least-32-bytes-long";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("orderpoint")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = _postgres.GetConnectionString(),
                ["Auth:Issuer"] = "https://orderpoint.local",
                ["Auth:Audience"] = "orderpoint-api",
                ["Auth:SigningKey"] = TestSigningKey,
                ["Outbox:PollingIntervalSeconds"] = "0.2",
                ["RateLimiting:AuthenticationPermitsPerMinute"] = "1000",
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var scripts = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "migrations"), "*.sql");
        Assert.NotEmpty(scripts);

        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();

        // Run twice: the second pass proves every script is idempotent.
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var script in scripts.Order())
            {
                await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(script), connection);
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PlatformCollection : ICollectionFixture<PlatformFixture>
{
    public const string Name = "Platform";
}
