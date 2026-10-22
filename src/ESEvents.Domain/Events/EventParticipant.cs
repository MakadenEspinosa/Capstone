namespace ESEvents.Domain.Events;

public sealed class EventParticipant
{
    public Guid EventId { get; private set; }
    public Guid UserId { get; private set; }
    public EventRole Role { get; private set; }

    private EventParticipant() { }

    internal EventParticipant(Guid eventId, Guid userId, EventRole role)
    {
        EventId = eventId;
        UserId = userId;
        Role = role;
    }

    internal void ChangeRole(EventRole role) => Role = role;
}