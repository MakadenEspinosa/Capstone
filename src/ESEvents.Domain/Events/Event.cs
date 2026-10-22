using ESEvents.Domain.Common;

namespace ESEvents.Domain.Events;

public sealed class Event : Entity
{
    private readonly List<EventParticipant> _participants = [];

    public string Title { get; private set; } = null!;
    public IReadOnlyCollection<EventParticipant> Participants => _participants;

    private Event() { }

    private Event(string title) => Title = title;

    public static Event Create(string title, Guid organizerUserId)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Event title is required.");
        if (organizerUserId == Guid.Empty) throw new DomainException("An organizer is required.");

        var @event = new Event(title.Trim());
        @event.AddParticipant(organizerUserId, EventRole.Organizer);
        return @event;
    }

    public void AddParticipant(Guid userId, EventRole role = EventRole.Member)
    {
        if (userId == Guid.Empty) throw new DomainException("A user is required.");
        if (_participants.Any(p => p.UserId == userId))
            throw new DomainException("User is already a participant in this event.");

        _participants.Add(new EventParticipant(Id, userId, role));
        Touch();
    }

    public void ChangeParticipantRole(Guid userId, EventRole role)
    {
        var participant = FindParticipant(userId);
        if (participant.Role == role) return;
        EnsureAnotherOrganizerRemains(participant);
        participant.ChangeRole(role);
        Touch();
    }

    public void RemoveParticipant(Guid userId)
    {
        var participant = FindParticipant(userId);
        EnsureAnotherOrganizerRemains(participant);
        _participants.Remove(participant);
        Touch();
    }

    private EventParticipant FindParticipant(Guid userId) =>
        _participants.FirstOrDefault(p => p.UserId == userId)
        ?? throw new DomainException("User is not a participant in this event.");

    private void EnsureAnotherOrganizerRemains(EventParticipant participant)
    {
        if (participant.Role == EventRole.Organizer &&
            !_participants.Any(p => p.UserId != participant.UserId && p.Role == EventRole.Organizer))
            throw new DomainException("An event must have at least one organizer.");
    }
}