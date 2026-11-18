using ESEvents.Application.Chat;
using ESEvents.Domain.Chat;
using Microsoft.EntityFrameworkCore;

namespace ESEvents.Infrastructure.Persistence.Chat;

internal sealed class ChatConversationRepository(AppDbContext db) : IChatConversationRepository
{
    public Task<Conversation?> GetAsync(
        Guid companyId, Guid userId1, Guid userId2, CancellationToken ct = default) =>
        db.ChatConversations.FirstOrDefaultAsync(conversation =>
            conversation.CompanyId == companyId &&
            conversation.UserId1 == userId1 &&
            conversation.UserId2 == userId2, ct);

    public async Task<Conversation> GetOrCreateAsync(
        Guid companyId, Guid userId1, Guid userId2, CancellationToken ct = default)
    {
        var existing = await GetAsync(companyId, userId1, userId2, ct);
        if (existing is not null) return existing;

        var conversation = Conversation.Create(companyId, userId1, userId2);
        await db.ChatConversations.AddAsync(conversation, ct);
        try
        {
            await db.SaveChangesAsync(ct);
            return conversation;
        }
        catch (DbUpdateException)
        {
            db.Entry(conversation).State = EntityState.Detached;
            var concurrent = await GetAsync(companyId, userId1, userId2, ct);
            if (concurrent is not null) return concurrent;
            throw;
        }
    }

    public async Task<IReadOnlyList<Conversation>> GetForUserAsync(
        Guid companyId,
        Guid userId,
        bool includeArchived,
        int pageSize,
        int pageNumber,
        CancellationToken ct = default) =>
        await db.ChatConversations
            .Where(conversation =>
                conversation.CompanyId == companyId &&
                (conversation.UserId1 == userId || conversation.UserId2 == userId) &&
                (includeArchived ||
                 (conversation.UserId1 == userId
                     ? !conversation.IsArchivedByUser1
                     : !conversation.IsArchivedByUser2)))
            .OrderByDescending(conversation => conversation.LastMessageTimestamp)
            .ThenByDescending(conversation => conversation.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(ct);

    public async Task SaveChangesAsync(CancellationToken ct = default) =>
        await db.SaveChangesAsync(ct);
}
