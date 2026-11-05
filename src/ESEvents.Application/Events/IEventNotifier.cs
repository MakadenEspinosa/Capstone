using ESEvents.Domain.Events;

namespace ESEvents.Application.Events;

public interface IEventNotifier
{
    Task NotifyInvitedAsync(
        Event @event,
        Guid actorUserId,
        IReadOnlyCollection<Guid> recipientUserIds,
        CancellationToken ct = default);

    Task NotifyUpdatedAsync(Event @event, EventSnapshot before, Guid actorUserId, CancellationToken ct = default);

    Task NotifyCancelledAsync(Event @event, Guid actorUserId, CancellationToken ct = default);
}

public sealed record EventSnapshot(
    string Title,
    string? Description,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    bool IsAllDay,
    Guid? DepartmentId)
{
    public static EventSnapshot From(Event @event) => new(
        @event.Title, @event.Description, @event.StartsAtUtc, @event.EndsAtUtc, @event.IsAllDay, @event.DepartmentId);
}
