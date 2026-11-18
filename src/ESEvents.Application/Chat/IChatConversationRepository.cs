using ESEvents.Domain.Chat;

namespace ESEvents.Application.Chat;

public interface IChatConversationRepository
{
    Task<Conversation?> GetAsync(Guid companyId, Guid userId1, Guid userId2, CancellationToken ct = default);
    Task<Conversation> GetOrCreateAsync(Guid companyId, Guid userId1, Guid userId2, CancellationToken ct = default);
    Task<IReadOnlyList<Conversation>> GetForUserAsync(
        Guid companyId, Guid userId, bool includeArchived, int pageSize, int pageNumber, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
