using ESEvents.Domain.Common;
using ESEvents.Domain.Users;

namespace ESEvents.Domain.Tests;

public class UserTests
{
    private static User NewUser() =>
        User.Register(Email.Create("Jane@Example.com"), "hash", UserProfile.Create("Jane", "Doe"));

    [Fact]
    public void Register_NormalizesEmail_AndIsActive()
    {
        var user = NewUser();
        Assert.Equal("jane@example.com", user.Email.Value);
        Assert.True(user.IsActive);
        Assert.Equal(SystemRole.User, user.SystemRole);
    }

    [Fact]
    public void Email_Invalid_Throws() =>
        Assert.Throws<DomainException>(() => Email.Create("not-an-email"));

    [Fact]
    public void Suspend_ThenReinstate()
    {
        var user = NewUser();
        user.Suspend();
        Assert.Equal(UserStatus.Suspended, user.Status);
        user.Reinstate();
        Assert.True(user.IsActive);
    }

    [Fact]
    public void RemovedUser_CannotBeModified()
    {
        var user = NewUser();
        user.Remove();
        Assert.Throws<DomainException>(() => user.Suspend());
    }
}
