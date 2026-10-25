using ESEvents.Application.Abstractions;
using ESEvents.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace ESEvents.Infrastructure.Security;

internal sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly Microsoft.AspNetCore.Identity.IPasswordHasher<ApplicationIdentityUser> _hasher;

    public IdentityPasswordHasher(Microsoft.AspNetCore.Identity.IPasswordHasher<ApplicationIdentityUser> hasher) =>
        _hasher = hasher;

    public string Hash(string password) => _hasher.HashPassword(new ApplicationIdentityUser(), password);

    public bool Verify(string password, string hash) =>
        _hasher.VerifyHashedPassword(new ApplicationIdentityUser(), hash, password) != PasswordVerificationResult.Failed;
}
