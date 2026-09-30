using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reporting.Domain;

namespace Reporting.Infrastructure.Persistence.Configurations;

public sealed class SalesRecordConfiguration : IEntityTypeConfiguration<SalesRecord>
{
    public void Configure(EntityTypeBuilder<SalesRecord> builder)
    {
        builder.ToTable("sales_records");
        builder.IsTenantScoped();

        builder.HasKey(r => r.OrderId);
        builder.Property(r => r.OrderId).ValueGeneratedNever();

        builder.Property(r => r.ConfirmedOnUtc).IsRequired();
        builder.Property(r => r.ConfirmedOnDate).IsRequired();
        builder.Property(r => r.Total).HasPrecision(14, 2).IsRequired();
        builder.Property(r => r.ItemCount).IsRequired();
        builder.Property(r => r.IsCancelled).IsRequired();

        builder.HasIndex(ModuleDbContext.TenantIdProperty, nameof(SalesRecord.ConfirmedOnDate));
    }
}
