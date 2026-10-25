using ESEvents.Application.Users;
using ESEvents.Domain.Users;
using ESEvents.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace ESEvents.Infrastructure.Persistence;

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default) =>
        (await db.Users
            .Where(u => u.Status != UserStatus.Removed)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(ct)).Select(ToDomain).ToArray();

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Status != UserStatus.Removed, ct);
        return user is null ? null : ToDomain(user);
    }

    public async Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default)
    {
        var normalizedEmail = email.Value.ToUpperInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);
        return user is null ? null : ToDomain(user);
    }

    public Task<bool> EmailExistsAsync(Email email, CancellationToken ct = default) =>
        db.Users.AnyAsync(u => u.NormalizedEmail == email.Value.ToUpperInvariant(), ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        var identityUser = new ApplicationIdentityUser
        {
            Id = user.Id,
            UserName = user.Email.Value,
            NormalizedUserName = user.Email.Value.ToUpperInvariant(),
            Email = user.Email.Value,
            NormalizedEmail = user.Email.Value.ToUpperInvariant(),
            PasswordHash = user.PasswordHash,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            LockoutEnabled = true,
            FirstName = user.Profile.FirstName,
            LastName = user.Profile.LastName,
            PhoneNumber = user.Profile.PhoneNumber,
            Bio = user.Profile.Bio,
            SystemRole = user.SystemRole,
            Status = user.Status,
            CreatedAtUtc = user.CreatedAtUtc,
            UpdatedAtUtc = user.UpdatedAtUtc
        };
        await db.Users.AddAsync(identityUser, ct);
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        var identityUser = await db.Users.FirstOrDefaultAsync(u => u.Id == user.Id, ct)
            ?? throw new InvalidOperationException("User no longer exists.");
        identityUser.FirstName = user.Profile.FirstName;
        identityUser.LastName = user.Profile.LastName;
        identityUser.PhoneNumber = user.Profile.PhoneNumber;
        identityUser.Bio = user.Profile.Bio;
        identityUser.SystemRole = user.SystemRole;
        identityUser.Status = user.Status;
        identityUser.UpdatedAtUtc = user.UpdatedAtUtc;
    }

    private static User ToDomain(ApplicationIdentityUser user) => User.Rehydrate(
        user.Id,
        Email.Create(user.Email),
        user.PasswordHash ?? string.Empty,
        UserProfile.Create(user.FirstName, user.LastName, user.PhoneNumber, user.Bio),
        user.SystemRole,
        user.Status,
        user.CreatedAtUtc,
        user.UpdatedAtUtc);
}
