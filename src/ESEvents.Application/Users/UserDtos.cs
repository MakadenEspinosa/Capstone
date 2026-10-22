using ESEvents.Domain.Users;

namespace ESEvents.Application.Users;

public sealed record RegisterUserRequest(string Email, string Password, string FirstName, string LastName);

public sealed record UserDto(Guid Id, string Email, string FirstName, string LastName, SystemRole SystemRole, UserStatus Status)
{
    public static UserDto From(User u) =>
        new(u.Id, u.Email.Value, u.Profile.FirstName, u.Profile.LastName, u.SystemRole, u.Status);
}
