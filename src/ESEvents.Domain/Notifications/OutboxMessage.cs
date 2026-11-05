using ESEvents.Domain.Common;

namespace ESEvents.Domain.Notifications;

public sealed class OutboxMessage : Entity
{
    public const int MaxAttempts = 5;
    public const int MaxSubjectLength = 200;
    public const int MaxErrorLength = 500;

    public string RecipientEmail { get; private set; } = null!;
    public string Subject { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public int Attempts { get; private set; }
    public DateTime NextAttemptAtUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }
    public DateTime? FailedAtUtc { get; private set; }
    public string? LastError { get; private set; }

    public bool IsPending => SentAtUtc is null && FailedAtUtc is null;

    private OutboxMessage() { }

    public static OutboxMessage Create(string? recipientEmail, string? subject, string? body)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail)) throw new DomainException("A recipient is required.");
        if (string.IsNullOrWhiteSpace(subject)) throw new DomainException("A subject is required.");
        if (string.IsNullOrWhiteSpace(body)) throw new DomainException("A message body is required.");

        return new OutboxMessage
        {
            RecipientEmail = recipientEmail.Trim(),
            Subject = SanitizeSubject(subject),
            Body = body,
            NextAttemptAtUtc = DateTime.UtcNow
        };
    }

    public void MarkSent(DateTime nowUtc)
    {
        SentAtUtc = nowUtc;
        LastError = null;
        Touch();
    }

    public void MarkFailed(string error, DateTime nowUtc)
    {
        Attempts++;
        LastError = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;

        if (Attempts >= MaxAttempts)
            FailedAtUtc = nowUtc;
        else
            NextAttemptAtUtc = nowUtc.AddMinutes(Math.Pow(2, Attempts));

        Touch();
    }

    // Line breaks in a subject would let a crafted event title inject mail headers.
    private static string SanitizeSubject(string subject)
    {
        var singleLine = string.Join(' ', subject.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        return singleLine.Length > MaxSubjectLength ? singleLine[..MaxSubjectLength] : singleLine;
    }
}
