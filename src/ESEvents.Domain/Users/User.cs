using ESEvents.Domain.Common;

namespace ESEvents.Domain.Users;

public sealed class User : Entity
{
    public Email Email { get; private set; }
    public string PasswordHash { get; private set; }
    public UserProfile Profile { get; private set; }
    public SystemRole SystemRole { get; private set; }
    public UserStatus Status { get; private set; }

    private User() { Email = null!; PasswordHash = null!; Profile = null!; }

    private User(Email email, string passwordHash, UserProfile profile, SystemRole systemRole)
    {
        Email = email;
        PasswordHash = passwordHash;
        Profile = profile;
        SystemRole = systemRole;
        Status = UserStatus.Active;
    }

    public static User Register(Email email, string passwordHash, UserProfile profile, SystemRole systemRole = SystemRole.User)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new DomainException("Password hash is required.");
        return new User(email, passwordHash, profile, systemRole);
    }

    public bool IsActive => Status == UserStatus.Active;

    public void UpdateProfile(UserProfile profile)
    {
        EnsureNotRemoved();
        Profile = profile;
        Touch();
    }

    public void ChangePasswordHash(string passwordHash)
    {
        EnsureNotRemoved();
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new DomainException("Password hash is required.");
        PasswordHash = passwordHash;
        Touch();
    }

    public void ChangeSystemRole(SystemRole systemRole)
    {
        EnsureNotRemoved();
        SystemRole = systemRole;
        Touch();
    }

    public void Suspend()
    {
        EnsureNotRemoved();
        Status = UserStatus.Suspended;
        Touch();
    }

    public void Reinstate()
    {
        EnsureNotRemoved();
        Status = UserStatus.Active;
        Touch();
    }

    public void Remove()
    {
        Status = UserStatus.Removed;
        Touch();
    }

    private void EnsureNotRemoved()
    {
        if (Status == UserStatus.Removed) throw new DomainException("User has been removed.");
    }
}
