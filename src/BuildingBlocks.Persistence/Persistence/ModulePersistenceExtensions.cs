using BuildingBlocks.Persistence.Outbox;
using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Persistence;

public static class ModulePersistenceExtensions
{
    public const string ConnectionStringName = "Database";
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>
    /// Registers a module's DbContext against the shared connection string,
    /// with the EF migrations history table inside the module's own schema
    /// (so migration state is independent per module), plus the module's
    /// <see cref="IOutboxStore"/> and a readiness health check.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string moduleName,
        string schema)
        where TContext : ModuleDbContext
    {
        services.AddDbContext<TContext>(options => options.UseNpgsql(
            configuration.GetConnectionString(ConnectionStringName),
            npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable, schema)));

        services.AddScoped<IOutboxStore>(sp => new ModuleOutboxStore<TContext>(sp.GetRequiredService<TContext>(), moduleName));

        services.AddHealthChecks().AddCheck<DbContextHealthCheck<TContext>>($"db-{schema}", tags: ["ready"]);

        return services;
    }

    /// <summary>
    /// Options for a module's <c>IDesignTimeDbContextFactory</c>: a fixed local
    /// connection string used only by <c>dotnet ef</c>, never at runtime.
    /// </summary>
    public static DbContextOptions<TContext> DesignTimeOptions<TContext>(string schema)
        where TContext : DbContext
        => new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=orderpoint;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable, schema))
            .Options;

    /// <summary>A tenant context with no tenant, for design-time factories.</summary>
    public static ITenantContext DesignTimeTenant { get; } = new TenantContext();
}
