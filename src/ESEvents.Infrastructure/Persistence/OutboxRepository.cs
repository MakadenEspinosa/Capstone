using ESEvents.Application.Notifications;
using ESEvents.Domain.Notifications;

namespace ESEvents.Infrastructure.Persistence;

internal sealed class OutboxRepository(AppDbContext db) : IOutboxRepository
{
    public async Task AddAsync(OutboxMessage message, CancellationToken ct = default) =>
        await db.OutboxMessages.AddAsync(message, ct);
}
