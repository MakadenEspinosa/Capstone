using ESEvents.Application.Notifications;
using ESEvents.Application.Users;
using ESEvents.Domain.Notifications;
using ESEvents.Domain.Users;

namespace ESEvents.Application.Tests;

internal sealed class FakeOutbox : IOutboxRepository
{
    public List<OutboxMessage> Messages { get; } = [];

    public Task AddAsync(OutboxMessage message, CancellationToken ct = default)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }
}

internal sealed class FakeUsers : IUserRepository
{
    public List<User> Store { get; } = [];

    public User Add(string first, string last, string email, bool suspended = false)
    {
        var user = User.Register(Email.Create(email), "hash", UserProfile.Create(first, last));
        if (suspended) user.Suspend();
        Store.Add(user);
        return user;
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Store.FirstOrDefault(u => u.Id == id));

    public Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<User>>(Store.Where(u => ids.Contains(u.Id)).ToArray());

    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<User>>(Store.ToArray());

    public Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default) =>
        Task.FromResult(Store.FirstOrDefault(u => u.Email == email));

    public Task<bool> EmailExistsAsync(Email email, CancellationToken ct = default) =>
        Task.FromResult(Store.Any(u => u.Email == email));

    public Task AddAsync(User user, CancellationToken ct = default) => throw new NotSupportedException();
    public Task UpdateAsync(User user, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
}
