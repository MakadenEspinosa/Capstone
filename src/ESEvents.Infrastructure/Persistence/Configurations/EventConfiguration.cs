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

        b.HasMany(e => e.Participants)
            .WithOne()
            .HasForeignKey(p => p.EventId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(e => e.Participants).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}