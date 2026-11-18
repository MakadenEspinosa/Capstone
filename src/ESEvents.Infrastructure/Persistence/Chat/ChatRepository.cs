using Cassandra;
using ESEvents.Application.Chat;
using ESEvents.Domain.Chat;
using ESEvents.Infrastructure.Persistence.Cassandra;
using Microsoft.Extensions.Logging;

namespace ESEvents.Infrastructure.Persistence.Chat;

public sealed class ChatRepository(
    CassandraSessionFactory sessionFactory,
    ILogger<ChatRepository> logger) : IChatRepository
{
    private const int MessageTtlSeconds = 2_592_000;

    public async Task SaveMessageAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = await sessionFactory.GetSessionAsync();
        var (user1, user2) = Normalize(message.SenderId, message.ReceiverId);
        var batch = new BatchStatement();
        batch.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);
        var createdAt = message.CreatedAtUtc;

        batch.Add(new SimpleStatement(
            $@"INSERT INTO chat_logs.messages_by_conversation
                (company_id, user_id_1, user_id_2, message_timestamp, message_id, sender_id,
                 receiver_id, content, created_at, is_read, is_deleted)
               VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, false, false) USING TTL {MessageTtlSeconds}",
            message.CompanyId, user1, user2, message.MessageTimestamp, message.Id,
            message.SenderId, message.ReceiverId, message.Content, createdAt));

        batch.Add(new SimpleStatement(
            $@"INSERT INTO chat_logs.messages_by_sender
                (company_id, sender_id, message_timestamp, message_id, receiver_id, content, created_at)
               VALUES (?, ?, ?, ?, ?, ?, ?) USING TTL {MessageTtlSeconds}",
            message.CompanyId, message.SenderId, message.MessageTimestamp, message.Id,
            message.ReceiverId, message.Content, createdAt));

        batch.Add(new SimpleStatement(
            $@"INSERT INTO chat_logs.unread_messages
                (company_id, receiver_id, sender_id, message_timestamp, message_id, content, created_at)
               VALUES (?, ?, ?, ?, ?, ?, ?) USING TTL {MessageTtlSeconds}",
            message.CompanyId, message.ReceiverId, message.SenderId, message.MessageTimestamp,
            message.Id, message.Content, createdAt));

        await session.ExecuteAsync(batch);
        logger.LogInformation("Saved chat message {MessageId} in company {CompanyId}", message.Id, message.CompanyId);
    }

    public async Task<IList<ChatMessage>> GetConversationAsync(
        Guid companyId,
        Guid userId1,
        Guid userId2,
        int pageSize = 50,
        long? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = await sessionFactory.GetSessionAsync();
        var (user1, user2) = Normalize(userId1, userId2);
        var query = @"SELECT * FROM chat_logs.messages_by_conversation
                      WHERE company_id = ? AND user_id_1 = ? AND user_id_2 = ?";
        if (pageToken.HasValue) query += " AND message_timestamp < ?";
        query += " LIMIT ?";

        var statement = pageToken.HasValue
            ? new SimpleStatement(query, companyId, user1, user2, pageToken.Value, pageSize)
            : new SimpleStatement(query, companyId, user1, user2, pageSize);
        statement.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);
        var result = await session.ExecuteAsync(statement);
        return result.Select(MapConversationMessage).ToList();
    }

    public async Task<int> GetUnreadCountAsync(
        Guid companyId,
        Guid receiverId,
        Guid? senderId = null,
        CancellationToken cancellationToken = default)
    {
        var unread = await GetUnreadRowsAsync(companyId, receiverId, cancellationToken);
        return unread.Count(row => !senderId.HasValue || row.GetValue<Guid>("sender_id") == senderId.Value);
    }

    public async Task MarkAsReadAsync(
        Guid companyId,
        Guid receiverId,
        Guid senderId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = await sessionFactory.GetSessionAsync();
        var unread = await GetUnreadRowsAsync(companyId, receiverId, cancellationToken);
        var matching = unread.Where(row => row.GetValue<Guid>("sender_id") == senderId).ToArray();
        if (matching.Length == 0) return;

        var (user1, user2) = Normalize(receiverId, senderId);
        var readAt = DateTime.UtcNow;
        var batch = new BatchStatement();
        batch.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);
        var pendingStatements = 0;
        foreach (var row in matching)
        {
            var timestamp = row.GetValue<long>("message_timestamp");
            var messageId = row.GetValue<Guid>("message_id");

            batch.Add(new SimpleStatement(
                @"DELETE FROM chat_logs.unread_messages
                  WHERE company_id = ? AND receiver_id = ? AND message_timestamp = ?
                    AND sender_id = ? AND message_id = ?",
                companyId, receiverId, timestamp, senderId, messageId));
            pendingStatements++;
            batch.Add(new SimpleStatement(
                @"UPDATE chat_logs.messages_by_conversation SET is_read = true, read_at_utc = ?
                  WHERE company_id = ? AND user_id_1 = ? AND user_id_2 = ?
                    AND message_timestamp = ? AND message_id = ?",
                readAt, companyId, user1, user2, timestamp, messageId));
            pendingStatements++;

            if (pendingStatements >= 100)
            {
                await session.ExecuteAsync(batch);
                batch = new BatchStatement();
                batch.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);
                pendingStatements = 0;
            }
        }

        if (pendingStatements > 0) await session.ExecuteAsync(batch);
    }

    public async Task<IList<ChatMessage>> GetSentMessagesAsync(
        Guid companyId,
        Guid senderId,
        int pageSize = 50,
        long? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = await sessionFactory.GetSessionAsync();
        var query = @"SELECT * FROM chat_logs.messages_by_sender
                      WHERE company_id = ? AND sender_id = ?";
        if (pageToken.HasValue) query += " AND message_timestamp < ?";
        query += " LIMIT ?";
        var statement = pageToken.HasValue
            ? new SimpleStatement(query, companyId, senderId, pageToken.Value, pageSize)
            : new SimpleStatement(query, companyId, senderId, pageSize);
        statement.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);
        var result = await session.ExecuteAsync(statement);
        return result.Select(MapSenderMessage).ToList();
    }

    public async Task DeleteMessageAsync(
        Guid companyId,
        Guid messageId,
        Guid userId1,
        Guid userId2,
        long messageTimestamp,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = await sessionFactory.GetSessionAsync();
        var (first, second) = Normalize(userId1, userId2);
        var statement = new SimpleStatement(
            @"UPDATE chat_logs.messages_by_conversation SET is_deleted = true
              WHERE company_id = ? AND user_id_1 = ? AND user_id_2 = ?
                AND message_timestamp = ? AND message_id = ?",
            companyId, first, second, messageTimestamp, messageId);
        statement.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);
        await session.ExecuteAsync(statement);
    }

    public async Task<IList<ChatMessage>> GetUnreadMessagesAsync(
        Guid companyId,
        Guid receiverId,
        int pageSize = 50,
        long? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = await sessionFactory.GetSessionAsync();
        var query = @"SELECT * FROM chat_logs.unread_messages
                      WHERE company_id = ? AND receiver_id = ?";
        if (pageToken.HasValue) query += " AND message_timestamp < ?";
        query += " LIMIT ?";
        var statement = pageToken.HasValue
            ? new SimpleStatement(query, companyId, receiverId, pageToken.Value, pageSize)
            : new SimpleStatement(query, companyId, receiverId, pageSize);
        statement.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);
        var result = await session.ExecuteAsync(statement);
        return result.Select(MapUnreadMessage).ToList();
    }

    public async Task<Dictionary<Guid, int>> GetUnreadBySenderAsync(
        Guid companyId, Guid receiverId, CancellationToken cancellationToken = default)
    {
        var rows = await GetUnreadRowsAsync(companyId, receiverId, cancellationToken);
        return rows.GroupBy(row => row.GetValue<Guid>("sender_id"))
            .ToDictionary(group => group.Key, group => group.Count());
    }

    private async Task<IReadOnlyList<Row>> GetUnreadRowsAsync(
        Guid companyId, Guid receiverId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = await sessionFactory.GetSessionAsync();
        var statement = new SimpleStatement(
            @"SELECT sender_id, message_timestamp, message_id FROM chat_logs.unread_messages
              WHERE company_id = ? AND receiver_id = ?",
            companyId, receiverId);
        statement.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);
        return (await session.ExecuteAsync(statement)).ToArray();
    }

    private static ChatMessage MapConversationMessage(Row row) => ChatMessage.CreateFromDatabase(
        row.GetValue<Guid>("message_id"),
        row.GetValue<Guid>("company_id"),
        row.GetValue<Guid>("sender_id"),
        row.GetValue<Guid>("receiver_id"),
        row.GetValue<string>("content") ?? string.Empty,
        row.GetValue<long>("message_timestamp"),
        row.GetValue<bool?>("is_read") ?? false,
        row.GetValue<DateTime?>("read_at_utc"),
        row.GetValue<bool?>("is_deleted") ?? false,
        row.GetValue<DateTime>("created_at"));

    private static ChatMessage MapSenderMessage(Row row) => ChatMessage.CreateFromDatabase(
        row.GetValue<Guid>("message_id"),
        row.GetValue<Guid>("company_id"),
        row.GetValue<Guid>("sender_id"),
        row.GetValue<Guid>("receiver_id"),
        row.GetValue<string>("content") ?? string.Empty,
        row.GetValue<long>("message_timestamp"),
        false,
        null,
        false,
        row.GetValue<DateTime>("created_at"));

    private static ChatMessage MapUnreadMessage(Row row) => ChatMessage.CreateFromDatabase(
        row.GetValue<Guid>("message_id"),
        row.GetValue<Guid>("company_id"),
        row.GetValue<Guid>("sender_id"),
        row.GetValue<Guid>("receiver_id"),
        row.GetValue<string>("content") ?? string.Empty,
        row.GetValue<long>("message_timestamp"),
        false,
        null,
        false,
        row.GetValue<DateTime>("created_at"));

    private static (Guid User1, Guid User2) Normalize(Guid userId1, Guid userId2) =>
        userId1 < userId2 ? (userId1, userId2) : (userId2, userId1);
}
