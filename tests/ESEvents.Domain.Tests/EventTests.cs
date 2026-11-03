using ESEvents.Domain.Common;
using ESEvents.Domain.Events;

namespace ESEvents.Domain.Tests;

public class EventTests
{
    private static Event NewEvent(Guid? departmentId = null, Guid? organizerUserId = null) =>
        Event.Create("Planning", DateTimeOffset.Parse("2026-11-02T10:00:00-05:00"),
            DateTimeOffset.Parse("2026-11-02T11:00:00-05:00"), false, organizerUserId ?? Guid.NewGuid(), departmentId);

    [Fact]
    public void Create_NormalizesTimesToUtcAndStoresDepartment()
    {
        var departmentId = Guid.NewGuid();
        var @event = NewEvent(departmentId);

        Assert.Equal(TimeSpan.Zero, @event.StartsAtUtc.Offset);
        Assert.Equal(TimeSpan.Zero, @event.EndsAtUtc.Offset);
        Assert.Equal(DateTimeOffset.Parse("2026-11-02T15:00:00Z"), @event.StartsAtUtc);
        Assert.Equal(departmentId, @event.DepartmentId);
    }

    [Fact]
    public void Create_RejectsInvalidTimeRange()
    {
        var timestamp = DateTimeOffset.UtcNow;
        Assert.Throws<DomainException>(() => Event.Create(
            "Planning", timestamp, timestamp, false, Guid.NewGuid()));
    }

    [Fact]
    public void UpdateDetails_RejectsEndBeforeStart()
    {
        var @event = NewEvent();

        Assert.Throws<DomainException>(() => @event.UpdateDetails(
            "Changed", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow, false, null));
    }

    [Fact]
    public void UserCanHaveDifferentRolesInDifferentEvents()
    {
        var userId = Guid.NewGuid();
        var firstEvent = NewEvent();
        var secondEvent = NewEvent();
        firstEvent.AddParticipant(userId, EventRole.Member);
        secondEvent.AddParticipant(userId, EventRole.Organizer);

        Assert.Equal(EventRole.Member, Assert.Single(firstEvent.Participants, p => p.UserId == userId).Role);
        Assert.Equal(EventRole.Organizer, Assert.Single(secondEvent.Participants, p => p.UserId == userId).Role);
    }

    [Fact]
    public void CannotRemoveLastOrganizer()
    {
        var organizerId = Guid.NewGuid();
        var @event = NewEvent(organizerUserId: organizerId);

        Assert.Throws<DomainException>(() => @event.RemoveParticipant(organizerId));
        Assert.Throws<DomainException>(() => @event.ChangeParticipantRole(organizerId, EventRole.Member));
    }

    [Fact]
    public void CannotAddDuplicateParticipant()
    {
        var userId = Guid.NewGuid();
        var @event = NewEvent();
        @event.AddParticipant(userId);

        Assert.Throws<DomainException>(() => @event.AddParticipant(userId, EventRole.Organizer));
    }
}