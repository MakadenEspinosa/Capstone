using ESEvents.Domain.Events;

namespace ESEvents.Application.Events;

public sealed record CreateEventRequest(
    string Title,
    DateTimeOffset Start,
    DateTimeOffset End,
    bool AllDay,
    Guid? DepartmentId,
    IReadOnlyList<Guid>? ParticipantUserIds,
    string? Description = null);

public sealed record UpdateEventRequest(
    string Title,
    DateTimeOffset Start,
    DateTimeOffset End,
    bool AllDay,
    Guid? DepartmentId,
    string? Description);

public sealed record UpdateEventParticipantRequest(EventRole Role);

public sealed record EventParticipantDto(Guid UserId, EventRole Role);

public sealed record CalendarEventDto(
    Guid Id,
    string Title,
    string? Description,
    DateTimeOffset Start,
    DateTimeOffset End,
    bool AllDay,
    Guid? DepartmentId,
    EventRole? CurrentUserRole,
    bool CanManage,
    IReadOnlyList<EventParticipantDto> Participants)
{
    public static CalendarEventDto From(Event @event, Guid currentUserId, bool isAdministrator) => new(
        @event.Id,
        @event.Title,
        @event.Description,
        @event.StartsAtUtc,
        @event.EndsAtUtc,
        @event.IsAllDay,
        @event.DepartmentId,
        @event.Participants.FirstOrDefault(p => p.UserId == currentUserId)?.Role,
        isAdministrator || @event.Participants.Any(p => p.UserId == currentUserId && p.Role == EventRole.Organizer),
        @event.Participants.Select(p => new EventParticipantDto(p.UserId, p.Role)).ToArray());
}
