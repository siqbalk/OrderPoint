using BuildingBlocks.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlocks.Persistence;

/// <summary>
/// Applied by <see cref="ModuleDbContext"/> to every module, so each module's
/// schema gets an identical <c>outbox_messages</c> table.
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type).HasMaxLength(300).IsRequired();
        builder.Property(m => m.Content).HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.OccurredOnUtc).IsRequired();
        builder.Property(m => m.Attempts).IsRequired().HasDefaultValue(0);
        builder.Property(m => m.Error).HasMaxLength(2000);

        // Partial index: the processor only ever scans unprocessed rows.
        builder.HasIndex(m => m.OccurredOnUtc)
            .HasDatabaseName("ix_outbox_messages_unprocessed")
            .HasFilter("\"ProcessedOnUtc\" IS NULL");
    }
}
