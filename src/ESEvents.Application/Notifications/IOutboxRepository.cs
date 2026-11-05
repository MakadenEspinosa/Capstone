using ESEvents.Domain.Notifications;

namespace ESEvents.Application.Notifications;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken ct = default);
}
