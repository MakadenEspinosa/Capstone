using ESEvents.Domain.Common;

namespace ESEvents.Domain.Users;

public sealed record UserProfile
{
    public string FirstName { get; }
    public string LastName { get; }
    public string? PhoneNumber { get; }
    public string? Bio { get; }

    private UserProfile(string firstName, string lastName, string? phoneNumber, string? bio)
    {
        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
        Bio = bio;
    }

    public static UserProfile Create(string? firstName, string? lastName, string? phoneNumber = null, string? bio = null)
    {
        if (string.IsNullOrWhiteSpace(firstName)) throw new DomainException("First name is required.");
        if (string.IsNullOrWhiteSpace(lastName)) throw new DomainException("Last name is required.");

        return new UserProfile(firstName.Trim(), lastName.Trim(), phoneNumber?.Trim(), bio?.Trim());
    }

    public string FullName => $"{FirstName} {LastName}";
}
