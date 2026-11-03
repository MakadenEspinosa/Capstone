using ESEvents.Domain.Events;

namespace ESEvents.Application.Events;

public interface IEventService
{
    Task<IReadOnlyList<CalendarEventDto>> GetCalendarEventsAsync(
        Guid userId,
        bool isAdministrator,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        CancellationToken ct = default);
    Task<CalendarEventDto?> GetAsync(Guid id, Guid userId, bool isAdministrator, CancellationToken ct = default);
    Task<CalendarEventDto> CreateAsync(Guid creatorUserId, bool isAdministrator, CreateEventRequest request, CancellationToken ct = default);
    Task<CalendarEventDto?> UpdateAsync(Guid id, Guid userId, bool isAdministrator, UpdateEventRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, bool isAdministrator, CancellationToken ct = default);
    Task<CalendarEventDto?> AddParticipantAsync(Guid id, Guid userId, bool isAdministrator, Guid participantUserId, EventRole role, CancellationToken ct = default);
    Task<CalendarEventDto?> ChangeParticipantRoleAsync(Guid id, Guid userId, bool isAdministrator, Guid participantUserId, EventRole role, CancellationToken ct = default);
    Task<CalendarEventDto?> RemoveParticipantAsync(Guid id, Guid userId, bool isAdministrator, Guid participantUserId, CancellationToken ct = default);
}
