using ESEvents.Application.Events;
using ESEvents.Domain.Events;

namespace ESEvents.Application.Tests;

public class EventNotifierTests
{
    private readonly FakeOutbox _outbox = new();
    private readonly FakeUsers _users = new();
    private readonly EventNotifier _notifier;

    public EventNotifierTests() => _notifier = new EventNotifier(_outbox, _users);

    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-11-05T15:00:00Z");

    private static Event NewEvent(Guid organizerId, string? description = null, params Guid[] members)
    {
        var @event = Event.Create("Roadmap review", Start, Start.AddHours(1), false, organizerId, null, description);
        foreach (var member in members) @event.AddParticipant(member);
        return @event;
    }

    [Fact]
    public async Task Invited_EmailsEachRecipient_ButNotTheActor()
    {
        var actor = _users.Add("Ann", "Lead", "ann@example.com");
        var bob = _users.Add("Bob", "Dev", "bob@example.com");
        var cara = _users.Add("Cara", "Ops", "cara@example.com");
        var @event = NewEvent(actor.Id, null, bob.Id, cara.Id);

        await _notifier.NotifyInvitedAsync(@event, actor.Id, [actor.Id, bob.Id, cara.Id]);

        Assert.Equal(["bob@example.com", "cara@example.com"], _outbox.Messages.Select(m => m.RecipientEmail).Order());
        Assert.All(_outbox.Messages, m => Assert.Equal("You're invited: Roadmap review", m.Subject));
        Assert.All(_outbox.Messages, m => Assert.Contains("Ann Lead added you to an event.", m.Body));
    }

    [Fact]
    public async Task Invited_SkipsSuspendedUsers_AndDuplicates()
    {
        var actor = _users.Add("Ann", "Lead", "ann@example.com");
        var bob = _users.Add("Bob", "Dev", "bob@example.com");
        var gone = _users.Add("Sam", "Away", "sam@example.com", suspended: true);
        var @event = NewEvent(actor.Id, null, bob.Id, gone.Id);

        await _notifier.NotifyInvitedAsync(@event, actor.Id, [bob.Id, bob.Id, gone.Id]);

        Assert.Equal("bob@example.com", Assert.Single(_outbox.Messages).RecipientEmail);
    }

    [Fact]
    public async Task Invited_IncludesDescriptionAndUtcTime()
    {
        var actor = _users.Add("Ann", "Lead", "ann@example.com");
        var bob = _users.Add("Bob", "Dev", "bob@example.com");
        var @event = NewEvent(actor.Id, "Bring the Q4 numbers.", bob.Id);

        await _notifier.NotifyInvitedAsync(@event, actor.Id, [bob.Id]);

        var body = Assert.Single(_outbox.Messages).Body;
        Assert.Contains("Bring the Q4 numbers.", body);
        Assert.Contains("Thu, Nov 5, 2026 3:00 PM to Thu, Nov 5, 2026 4:00 PM UTC", body);
    }

    [Fact]
    public async Task Invited_WithOnlyTheActor_SendsNothing()
    {
        var actor = _users.Add("Ann", "Lead", "ann@example.com");

        await _notifier.NotifyInvitedAsync(NewEvent(actor.Id), actor.Id, [actor.Id]);

        Assert.Empty(_outbox.Messages);
    }

    [Fact]
    public async Task Updated_WithNoRealChange_SendsNothing()
    {
        var actor = _users.Add("Ann", "Lead", "ann@example.com");
        var bob = _users.Add("Bob", "Dev", "bob@example.com");
        var @event = NewEvent(actor.Id, "Same", bob.Id);
        var before = EventSnapshot.From(@event);

        @event.UpdateDetails("Roadmap review", Start, Start.AddHours(1), false, null, "Same");
        await _notifier.NotifyUpdatedAsync(@event, before, actor.Id);

        Assert.Empty(_outbox.Messages);
    }

    [Fact]
    public async Task Updated_ListsOnlyWhatChanged_AndSkipsTheActor()
    {
        var actor = _users.Add("Ann", "Lead", "ann@example.com");
        var bob = _users.Add("Bob", "Dev", "bob@example.com");
        var @event = NewEvent(actor.Id, null, bob.Id);
        var before = EventSnapshot.From(@event);

        @event.UpdateDetails("Roadmap review v2", Start.AddHours(2), Start.AddHours(3), false, null, null);
        await _notifier.NotifyUpdatedAsync(@event, before, actor.Id);

        var message = Assert.Single(_outbox.Messages);
        Assert.Equal("bob@example.com", message.RecipientEmail);
        Assert.Equal("Event updated: Roadmap review v2", message.Subject);
        Assert.Contains("Title: \"Roadmap review\" is now \"Roadmap review v2\"", message.Body);
        Assert.Contains("When:", message.Body);
        Assert.DoesNotContain("Description", message.Body);
    }

    [Fact]
    public async Task Updated_ReportsDescriptionRemovedAndDepartmentChanged()
    {
        var actor = _users.Add("Ann", "Lead", "ann@example.com");
        var bob = _users.Add("Bob", "Dev", "bob@example.com");
        var @event = NewEvent(actor.Id, "Old text", bob.Id);
        var before = EventSnapshot.From(@event);

        @event.UpdateDetails("Roadmap review", Start, Start.AddHours(1), false, Guid.NewGuid(), null);
        await _notifier.NotifyUpdatedAsync(@event, before, actor.Id);

        var body = Assert.Single(_outbox.Messages).Body;
        Assert.Contains("Description was removed", body);
        Assert.Contains("Department tag was changed", body);
    }

    [Fact]
    public async Task Cancelled_EmailsParticipantsExceptTheActor()
    {
        var actor = _users.Add("Ann", "Lead", "ann@example.com");
        var bob = _users.Add("Bob", "Dev", "bob@example.com");

        await _notifier.NotifyCancelledAsync(NewEvent(actor.Id, null, bob.Id), actor.Id);

        var message = Assert.Single(_outbox.Messages);
        Assert.Equal("bob@example.com", message.RecipientEmail);
        Assert.Equal("Event cancelled: Roadmap review", message.Subject);
        Assert.Contains("cancelled an event you were part of", message.Body);
    }

    [Fact]
    public async Task UnknownActor_FallsBackToSomeone()
    {
        var bob = _users.Add("Bob", "Dev", "bob@example.com");

        await _notifier.NotifyInvitedAsync(NewEvent(bob.Id), Guid.NewGuid(), [bob.Id]);

        Assert.Contains("Someone added you to an event.", Assert.Single(_outbox.Messages).Body);
    }

    [Fact]
    public async Task EventTitleWithLineBreak_CannotInjectSubjectHeaders()
    {
        var actor = _users.Add("Ann", "Lead", "ann@example.com");
        var bob = _users.Add("Bob", "Dev", "bob@example.com");
        var @event = Event.Create("Hi\r\nBcc: evil@example.com", Start, Start.AddHours(1), false, actor.Id);
        @event.AddParticipant(bob.Id);

        await _notifier.NotifyInvitedAsync(@event, actor.Id, [bob.Id]);

        Assert.DoesNotContain('\n', Assert.Single(_outbox.Messages).Subject);
    }
}
