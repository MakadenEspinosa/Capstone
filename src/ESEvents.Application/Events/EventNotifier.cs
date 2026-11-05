using System.Globalization;
using System.Text;
using ESEvents.Application.Notifications;
using ESEvents.Application.Users;
using ESEvents.Domain.Events;
using ESEvents.Domain.Notifications;
using ESEvents.Domain.Users;

namespace ESEvents.Application.Events;

public sealed class EventNotifier(IOutboxRepository outbox, IUserRepository users) : IEventNotifier
{
    public async Task NotifyInvitedAsync(
        Event @event,
        Guid actorUserId,
        IReadOnlyCollection<Guid> recipientUserIds,
        CancellationToken ct = default)
    {
        var actorName = await ActorNameAsync(actorUserId, ct);
        var body = new StringBuilder()
            .AppendLine($"{actorName} added you to an event.")
            .AppendLine()
            .Append(Describe(@event))
            .ToString();

        await QueueAsync(recipientUserIds, actorUserId, $"You're invited: {@event.Title}", body, ct);
    }

    public async Task NotifyUpdatedAsync(Event @event, EventSnapshot before, Guid actorUserId, CancellationToken ct = default)
    {
        var changes = DescribeChanges(before, EventSnapshot.From(@event));
        if (changes.Count == 0) return;

        var actorName = await ActorNameAsync(actorUserId, ct);
        var body = new StringBuilder()
            .AppendLine($"{actorName} updated an event you're part of.")
            .AppendLine()
            .AppendLine("What changed:");
        foreach (var change in changes)
            body.AppendLine($"  - {change}");
        body.AppendLine().Append(Describe(@event));

        await QueueAsync(
            @event.Participants.Select(p => p.UserId).ToArray(),
            actorUserId,
            $"Event updated: {@event.Title}",
            body.ToString(),
            ct);
    }

    public async Task NotifyCancelledAsync(Event @event, Guid actorUserId, CancellationToken ct = default)
    {
        var actorName = await ActorNameAsync(actorUserId, ct);
        var body = new StringBuilder()
            .AppendLine($"{actorName} cancelled an event you were part of.")
            .AppendLine()
            .Append(Describe(@event))
            .ToString();

        await QueueAsync(
            @event.Participants.Select(p => p.UserId).ToArray(),
            actorUserId,
            $"Event cancelled: {@event.Title}",
            body,
            ct);
    }

    private async Task QueueAsync(
        IReadOnlyCollection<Guid> recipientUserIds,
        Guid actorUserId,
        string subject,
        string body,
        CancellationToken ct)
    {
        var ids = recipientUserIds.Where(id => id != actorUserId).Distinct().ToArray();
        if (ids.Length == 0) return;

        foreach (var recipient in (await users.GetByIdsAsync(ids, ct)).Where(u => u.IsActive))
            await outbox.AddAsync(OutboxMessage.Create(recipient.Email.Value, subject, body), ct);
    }

    private async Task<string> ActorNameAsync(Guid actorUserId, CancellationToken ct)
    {
        var actor = await users.GetByIdAsync(actorUserId, ct);
        return actor is null ? "Someone" : $"{actor.Profile.FirstName} {actor.Profile.LastName}";
    }

    private static List<string> DescribeChanges(EventSnapshot before, EventSnapshot after)
    {
        var changes = new List<string>();
        if (before.Title != after.Title)
            changes.Add($"Title: \"{before.Title}\" is now \"{after.Title}\"");
        if (before.StartsAtUtc != after.StartsAtUtc || before.EndsAtUtc != after.EndsAtUtc || before.IsAllDay != after.IsAllDay)
            changes.Add($"When: {FormatRange(before)} is now {FormatRange(after)}");
        if (before.Description != after.Description)
            changes.Add(after.Description is null ? "Description was removed" : "Description was updated");
        if (before.DepartmentId != after.DepartmentId)
            changes.Add("Department tag was changed");
        return changes;
    }

    private static string Describe(Event @event)
    {
        var text = new StringBuilder()
            .AppendLine($"Event: {@event.Title}")
            .AppendLine($"When:  {FormatRange(EventSnapshot.From(@event))}");
        if (!string.IsNullOrWhiteSpace(@event.Description))
            text.AppendLine().AppendLine(@event.Description);
        text.AppendLine().AppendLine("All times are shown in UTC.");
        return text.ToString();
    }

    private static string FormatRange(EventSnapshot snapshot)
    {
        var start = snapshot.StartsAtUtc.ToUniversalTime().ToString("ddd, MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture);
        var end = snapshot.EndsAtUtc.ToUniversalTime().ToString("ddd, MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture);
        return $"{start} to {end} UTC{(snapshot.IsAllDay ? " (all-day event)" : string.Empty)}";
    }
}
