using ESEvents.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace ESEvents.Infrastructure.Identity;

public sealed class ApplicationIdentityUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public SystemRole SystemRole { get; set; } = SystemRole.User;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}