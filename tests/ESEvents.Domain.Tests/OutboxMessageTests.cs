using ESEvents.Domain.Common;
using ESEvents.Domain.Notifications;

namespace ESEvents.Domain.Tests;

public class OutboxMessageTests
{
    private static OutboxMessage NewMessage() => OutboxMessage.Create("jane@example.com", "Hello", "Body");

    [Fact]
    public void Create_StartsPendingAndDueNow()
    {
        var message = NewMessage();

        Assert.True(message.IsPending);
        Assert.Equal(0, message.Attempts);
        Assert.True(message.NextAttemptAtUtc <= DateTime.UtcNow);
    }

    [Theory]
    [InlineData(null, "Subject", "Body")]
    [InlineData("  ", "Subject", "Body")]
    [InlineData("a@b.com", "", "Body")]
    [InlineData("a@b.com", "Subject", " ")]
    public void Create_RequiresRecipientSubjectAndBody(string? to, string? subject, string? body) =>
        Assert.Throws<DomainException>(() => OutboxMessage.Create(to, subject, body));

    [Fact]
    public void Create_StripsLineBreaksFromSubject()
    {
        var message = OutboxMessage.Create("a@b.com", "Planning\r\nBcc: attacker@example.com", "Body");

        Assert.DoesNotContain('\n', message.Subject);
        Assert.DoesNotContain('\r', message.Subject);
    }

    [Fact]
    public void Create_TruncatesLongSubject()
    {
        var message = OutboxMessage.Create("a@b.com", new string('x', 500), "Body");

        Assert.Equal(OutboxMessage.MaxSubjectLength, message.Subject.Length);
    }

    [Fact]
    public void MarkSent_ClearsPendingAndError()
    {
        var message = NewMessage();
        message.MarkFailed("boom", DateTime.UtcNow);

        message.MarkSent(DateTime.UtcNow);

        Assert.False(message.IsPending);
        Assert.Null(message.LastError);
    }

    [Fact]
    public void MarkFailed_BacksOffThenGivesUpAfterMaxAttempts()
    {
        var message = NewMessage();
        var now = DateTime.UtcNow;

        message.MarkFailed("smtp down", now);
        Assert.True(message.IsPending);
        Assert.Equal(now.AddMinutes(2), message.NextAttemptAtUtc);

        message.MarkFailed("smtp down", now);
        Assert.Equal(now.AddMinutes(4), message.NextAttemptAtUtc);

        for (var i = message.Attempts; i < OutboxMessage.MaxAttempts; i++)
            message.MarkFailed("smtp down", now);

        Assert.False(message.IsPending);
        Assert.NotNull(message.FailedAtUtc);
        Assert.Equal("smtp down", message.LastError);
    }

    [Fact]
    public void MarkFailed_TruncatesLongErrors()
    {
        var message = NewMessage();

        message.MarkFailed(new string('e', 2000), DateTime.UtcNow);

        Assert.Equal(OutboxMessage.MaxErrorLength, message.LastError!.Length);
    }
}
