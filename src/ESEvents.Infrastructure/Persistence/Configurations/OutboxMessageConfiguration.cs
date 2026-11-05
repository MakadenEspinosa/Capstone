using ESEvents.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ESEvents.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox_messages");
        b.HasKey(m => m.Id);
        b.Property(m => m.RecipientEmail).HasMaxLength(320).IsRequired();
        b.Property(m => m.Subject).HasMaxLength(OutboxMessage.MaxSubjectLength).IsRequired();
        b.Property(m => m.Body).IsRequired();
        b.Property(m => m.LastError).HasMaxLength(OutboxMessage.MaxErrorLength);
        b.Ignore(m => m.IsPending);
        b.HasIndex(m => m.NextAttemptAtUtc)
            .HasFilter("\"SentAtUtc\" IS NULL AND \"FailedAtUtc\" IS NULL");
    }
}
