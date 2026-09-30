using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sales.Domain;

namespace Sales.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.IsTenantScoped();

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.CustomerName).HasMaxLength(200).IsRequired();
        builder.Property(o => o.CustomerEmail).HasMaxLength(256).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(o => o.Total).HasPrecision(14, 2).IsRequired();
        builder.Property(o => o.PlacedOnUtc).IsRequired();
        builder.Property(o => o.RejectionReason).HasMaxLength(500);

        builder.HasIndex(ModuleDbContext.TenantIdProperty, nameof(Order.PlacedOnUtc));
        builder.HasIndex(ModuleDbContext.TenantIdProperty, nameof(Order.Status));

        builder.OwnsMany(o => o.Lines, lines =>
        {
            lines.ToTable("order_lines");
            lines.WithOwner().HasForeignKey("OrderId");
            lines.HasKey(l => l.Id);
            lines.Property(l => l.Id).ValueGeneratedNever();
            lines.Property(l => l.Sku).HasMaxLength(50).IsRequired();
            lines.Property(l => l.ProductName).HasMaxLength(200).IsRequired();
            lines.Property(l => l.Quantity).IsRequired();
            lines.Property(l => l.UnitPrice).HasPrecision(12, 2).IsRequired();
            lines.Ignore(l => l.LineTotal);
        });
        builder.Navigation(o => o.Lines).AutoInclude();
    }
}
