using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.IsTenantScoped();

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.Channel).HasMaxLength(20).IsRequired();
        builder.Property(n => n.Recipient).HasMaxLength(256).IsRequired();
        builder.Property(n => n.Subject).HasMaxLength(300).IsRequired();
        builder.Property(n => n.Body).IsRequired();
        builder.Property(n => n.SentOnUtc).IsRequired();

        // Idempotency key for outbox redeliveries.
        builder.HasIndex(n => new { n.SourceEventId, n.Recipient }).IsUnique();
        builder.HasIndex(ModuleDbContext.TenantIdProperty, nameof(Notification.SentOnUtc));
    }
}
