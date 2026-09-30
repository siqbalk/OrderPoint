using System.Reflection;
using System.Text.Json;
using BuildingBlocks.Messaging;
using BuildingBlocks.MultiTenancy;
using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BuildingBlocks.Persistence;

/// <summary>
/// Base class for every module's DbContext. It supplies the three things each
/// module would otherwise reimplement identically:
/// <list type="bullet">
/// <item>the module's own schema (<see cref="Schema"/>) and outbox table;</item>
/// <item><see cref="IOutboxWriter"/>, so integration events are saved in the same transaction as the business change;</item>
/// <item>tenant isolation: every entity configured with <see cref="TenantScopingExtensions.IsTenantScoped{T}"/>
/// gets a <c>TenantId</c> shadow column, a query filter on the current tenant, and
/// the tenant stamped on insert. Queries with no tenant in scope return nothing (fail closed).</item>
/// </list>
/// </summary>
public abstract class ModuleDbContext : DbContext, IOutboxWriter
{
    public const string TenantIdProperty = "TenantId";
    internal const string TenantScopedAnnotation = "BuildingBlocks:TenantScoped";

    private static readonly MethodInfo ApplyTenantFilterMethod =
        typeof(ModuleDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly ITenantContext _tenantContext;

    protected ModuleDbContext(DbContextOptions options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>The PostgreSQL schema this module owns exclusively.</summary>
    protected abstract string Schema { get; }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    // Read by the tenant query filters. EF Core evaluates this per query against
    // the executing context instance, not once when the model is built.
    private Guid? CurrentTenantId => _tenantContext.TenantId;

    /// <summary>For raw SQL that must stamp the tenant itself (the query filter does not apply to it).</summary>
    protected Guid RequiredTenantId => _tenantContext.RequiredTenantId;

    public void Enqueue(IIntegrationEvent integrationEvent)
    {
        var eventType = integrationEvent.GetType();
        OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = eventType.FullName!,
            Content = JsonSerializer.Serialize(integrationEvent, eventType),
            OccurredOnUtc = integrationEvent.OccurredOnUtc,
        });
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(IsTenantScoped).ToList())
        {
            ApplyTenantFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTenant();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTenant();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class
        => modelBuilder.Entity<TEntity>().HasQueryFilter(e => EF.Property<Guid>(e, TenantIdProperty) == CurrentTenantId);

    private void StampTenant()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added && IsTenantScoped(e.Metadata)))
        {
            var tenantProperty = entry.Property(TenantIdProperty);
            if (tenantProperty.CurrentValue is Guid existing && existing != Guid.Empty)
            {
                continue;
            }

            tenantProperty.CurrentValue = _tenantContext.TenantId
                ?? throw new InvalidOperationException(
                    $"Cannot save a new {entry.Metadata.ClrType.Name} without a tenant in scope.");
        }
    }

    private static bool IsTenantScoped(IReadOnlyEntityType entityType)
        => entityType.FindAnnotation(TenantScopedAnnotation)?.Value is true;
}
