using ESEvents.Domain.Users;

namespace ESEvents.Application.Users;

public interface IUserRepository
{
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(Email email, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
}
