using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlocks.Persistence;

public static class TenantScopingExtensions
{
    /// <summary>
    /// Marks an aggregate root as belonging to a tenant: adds a required
    /// <c>TenantId</c> shadow column that <see cref="ModuleDbContext"/> filters
    /// on and stamps automatically. The domain model never sees the tenant.
    /// Composite indexes that include the tenant reference it by name:
    /// <c>builder.HasIndex(ModuleDbContext.TenantIdProperty, nameof(Product.Sku)).IsUnique()</c>.
    /// </summary>
    public static EntityTypeBuilder<T> IsTenantScoped<T>(this EntityTypeBuilder<T> builder)
        where T : class
    {
        builder.Property<Guid>(ModuleDbContext.TenantIdProperty).IsRequired();
        builder.HasAnnotation(ModuleDbContext.TenantScopedAnnotation, true);
        return builder;
    }
}
