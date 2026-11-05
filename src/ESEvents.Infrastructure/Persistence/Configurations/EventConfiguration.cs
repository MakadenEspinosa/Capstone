using ESEvents.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ESEvents.Infrastructure.Persistence.Configurations;

internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> b)
    {
        b.ToTable("events");
        b.HasKey(e => e.Id);
        b.Property(e => e.Title).HasMaxLength(200).IsRequired();
        b.Property(e => e.Description).HasMaxLength(Event.MaxDescriptionLength);
        b.Property(e => e.StartsAtUtc).IsRequired();
        b.Property(e => e.EndsAtUtc).IsRequired();
        b.Property(e => e.IsAllDay).IsRequired();
        b.HasIndex(e => new { e.StartsAtUtc, e.EndsAtUtc });
        b.HasOne<Domain.Departments.Department>()
            .WithMany()
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasMany(e => e.Participants)
            .WithOne()
            .HasForeignKey(p => p.EventId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(e => e.Participants).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}