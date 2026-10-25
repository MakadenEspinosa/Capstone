using ESEvents.Application.Users;
using ESEvents.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ESEvents.Infrastructure.Persistence;

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default) =>
        await db.Users
            .Where(u => u.Status != UserStatus.Removed)
            .OrderBy(u => u.Profile.LastName)
            .ThenBy(u => u.Profile.FirstName)
            .ToListAsync(ct);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Status != UserStatus.Removed, ct);

    public Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<bool> EmailExistsAsync(Email email, CancellationToken ct = default) =>
        db.Users.AnyAsync(u => u.Email == email, ct);

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await db.Users.AddAsync(user, ct);
}
