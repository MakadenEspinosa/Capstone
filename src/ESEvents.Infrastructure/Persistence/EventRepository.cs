using ESEvents.Application.Events;
using ESEvents.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace ESEvents.Infrastructure.Persistence;

internal sealed class EventRepository(AppDbContext db) : IEventRepository
{
    public async Task<IReadOnlyList<Event>> GetVisibleInRangeAsync(
        Guid userId,
        bool isAdministrator,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        CancellationToken ct = default)
    {
        var query = db.Events
            .Where(e => e.StartsAtUtc < rangeEnd && e.EndsAtUtc > rangeStart);

        if (!isAdministrator)
        {
            query = query.Where(e =>
                e.Participants.Any(p => p.UserId == userId) ||
                (e.DepartmentId != null && db.Departments.Any(d =>
                    d.Id == e.DepartmentId && d.Members.Any(m => m.UserId == userId))));
        }

        return await query
            .Include(e => e.Participants)
            .OrderBy(e => e.StartsAtUtc)
            .ToListAsync(ct);
    }

    public Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Events.Include(e => e.Participants).FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task AddAsync(Event @event, CancellationToken ct = default) =>
        await db.Events.AddAsync(@event, ct);

    public Task RemoveAsync(Event @event, CancellationToken ct = default)
    {
        db.Events.Remove(@event);
        return Task.CompletedTask;
    }
}
