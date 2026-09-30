using BuildingBlocks.Persistence;
using Inventory.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("stock_items", t =>
        {
            t.HasCheckConstraint("ck_stock_items_on_hand_non_negative", "\"QuantityOnHand\" >= 0");
            t.HasCheckConstraint("ck_stock_items_reserved_within_on_hand", "\"QuantityReserved\" >= 0 AND \"QuantityReserved\" <= \"QuantityOnHand\"");
        });
        builder.IsTenantScoped();

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Sku).HasMaxLength(50).IsRequired();
        builder.HasIndex(ModuleDbContext.TenantIdProperty, nameof(StockItem.Sku)).IsUnique();

        builder.Property(s => s.QuantityOnHand).IsRequired();
        builder.Property(s => s.QuantityReserved).IsRequired();
        builder.Property(s => s.UpdatedOnUtc).IsRequired();

        // Optimistic concurrency on PostgreSQL's system column: two orders reserving
        // the same SKU concurrently cannot both succeed against a stale quantity.
        // A failed save surfaces as DbUpdateConcurrencyException: the outbox retries
        // the event against fresh quantities; an HTTP caller gets 409 Conflict.
        builder.Property<uint>("RowVersion").IsRowVersion();

        builder.Ignore(s => s.QuantityAvailable);
    }
}
