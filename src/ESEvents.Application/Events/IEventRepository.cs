using ESEvents.Domain.Events;

namespace ESEvents.Application.Events;

public interface IEventRepository
{
    Task<IReadOnlyList<Event>> GetVisibleInRangeAsync(
        Guid userId,
        bool isAdministrator,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        CancellationToken ct = default);
    Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Event @event, CancellationToken ct = default);
    Task RemoveAsync(Event @event, CancellationToken ct = default);
}
