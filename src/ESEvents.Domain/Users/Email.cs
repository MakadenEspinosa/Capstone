using System.Net.Mail;
using ESEvents.Domain.Common;

namespace ESEvents.Domain.Users;

public sealed record Email
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string? value)
    {
        var trimmed = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(trimmed) || !MailAddress.TryCreate(trimmed, out var addr) || addr.Address != trimmed)
            throw new DomainException("Email address is invalid.");

        return new Email(trimmed);
    }

    public override string ToString() => Value;
}
