using ESEvents.Domain.Common;

namespace ESEvents.Domain.Events;

public sealed class Event : Entity
{
    public const int MaxDescriptionLength = 1000;

    private readonly List<EventParticipant> _participants = [];

    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset EndsAtUtc { get; private set; }
    public bool IsAllDay { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public IReadOnlyCollection<EventParticipant> Participants => _participants;

    private Event() { }

    private Event(string title) => Title = title;

    public static Event Create(
        string title,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        bool isAllDay,
        Guid organizerUserId,
        Guid? departmentId = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Event title is required.");
        if (organizerUserId == Guid.Empty) throw new DomainException("An organizer is required.");
        ValidateSchedule(startsAt, endsAt);
        if (departmentId == Guid.Empty) throw new DomainException("Department id cannot be empty.");

        var @event = new Event(title.Trim())
        {
            Description = NormalizeDescription(description),
            StartsAtUtc = startsAt.ToUniversalTime(),
            EndsAtUtc = endsAt.ToUniversalTime(),
            IsAllDay = isAllDay,
            DepartmentId = departmentId
        };
        @event.AddParticipant(organizerUserId, EventRole.Organizer);
        return @event;
    }

    public void UpdateDetails(
        string title,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        bool isAllDay,
        Guid? departmentId,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Event title is required.");
        ValidateSchedule(startsAt, endsAt);
        if (departmentId == Guid.Empty) throw new DomainException("Department id cannot be empty.");
        var normalizedDescription = NormalizeDescription(description);

        Title = title.Trim();
        Description = normalizedDescription;
        StartsAtUtc = startsAt.ToUniversalTime();
        EndsAtUtc = endsAt.ToUniversalTime();
        IsAllDay = isAllDay;
        DepartmentId = departmentId;
        Touch();
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

    private static void ValidateSchedule(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        if (endsAt <= startsAt)
            throw new DomainException("Event end must be after its start.");
    }

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmed is { Length: > MaxDescriptionLength })
            throw new DomainException($"Event description cannot exceed {MaxDescriptionLength} characters.");
        return trimmed;
    }
}