using BuildingBlocks.Persistence;
using Inventory.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> builder)
    {
        builder.ToTable("stock_reservations");
        builder.IsTenantScoped();

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        // One reservation per order: the idempotency key for OrderPlaced redeliveries.
        builder.HasIndex(ModuleDbContext.TenantIdProperty, nameof(StockReservation.OrderId)).IsUnique();

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.CreatedOnUtc).IsRequired();

        builder.OwnsMany(r => r.Lines, lines =>
        {
            lines.ToTable("stock_reservation_lines");
            lines.WithOwner().HasForeignKey("ReservationId");
            lines.Property<int>("Id");
            lines.HasKey("Id");
            lines.Property(l => l.Sku).HasMaxLength(50).IsRequired();
            lines.Property(l => l.Quantity).IsRequired();
        });
        builder.Navigation(r => r.Lines).AutoInclude();
    }
}
