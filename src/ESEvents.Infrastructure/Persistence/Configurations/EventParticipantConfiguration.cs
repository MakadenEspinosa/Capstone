using ESEvents.Domain.Events;
using ESEvents.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ESEvents.Infrastructure.Persistence.Configurations;

internal sealed class EventParticipantConfiguration : IEntityTypeConfiguration<EventParticipant>
{
    public void Configure(EntityTypeBuilder<EventParticipant> b)
    {
        b.ToTable("event_participants");
        b.HasKey(p => new { p.EventId, p.UserId });
        b.Property(p => p.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        b.HasOne<ApplicationIdentityUser>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}