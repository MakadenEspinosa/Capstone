using ESEvents.Domain.Common;
using ESEvents.Domain.Events;

namespace ESEvents.Domain.Tests;

public class EventTests
{
    [Fact]
    public void UserCanHaveDifferentRolesInDifferentEvents()
    {
        var userId = Guid.NewGuid();
        var firstEvent = Event.Create("First", Guid.NewGuid());
        var secondEvent = Event.Create("Second", Guid.NewGuid());
        firstEvent.AddParticipant(userId, EventRole.Member);
        secondEvent.AddParticipant(userId, EventRole.Organizer);

        Assert.Equal(EventRole.Member, Assert.Single(firstEvent.Participants, p => p.UserId == userId).Role);
        Assert.Equal(EventRole.Organizer, Assert.Single(secondEvent.Participants, p => p.UserId == userId).Role);
    }

    [Fact]
    public void CannotRemoveLastOrganizer()
    {
        var organizerId = Guid.NewGuid();
        var @event = Event.Create("Planning", organizerId);

        Assert.Throws<DomainException>(() => @event.RemoveParticipant(organizerId));
        Assert.Throws<DomainException>(() => @event.ChangeParticipantRole(organizerId, EventRole.Member));
    }

    [Fact]
    public void CannotAddDuplicateParticipant()
    {
        var userId = Guid.NewGuid();
        var @event = Event.Create("Planning", Guid.NewGuid());
        @event.AddParticipant(userId);

        Assert.Throws<DomainException>(() => @event.AddParticipant(userId, EventRole.Organizer));
    }
}