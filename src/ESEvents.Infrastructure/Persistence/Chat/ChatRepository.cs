using Cassandra;
using ESEvents.Application.Chat;
using ESEvents.Domain.Chat;
using ESEvents.Infrastructure.Persistence.Cassandra;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ESEvents.Infrastructure.Persistence.Chat;

/// <summary>
/// Cassandra implementation of chat repository.
/// Manages persistence of chat messages across three denormalized tables.
/// </summary>
public class ChatRepository : IChatRepository
{
    private readonly CassandraSessionFactory _sessionFactory;
    private readonly ILogger<ChatRepository> _logger;
    private const string Keyspace = "chat_logs";

    public ChatRepository(CassandraSessionFactory sessionFactory, ILogger<ChatRepository> logger)
    {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task SaveMessageAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await _sessionFactory.GetSessionAsync();

            // Prepare the batch insert to write to all 3 tables atomically
            var batch = new BatchStatement();
            batch.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);

            // Normalize participant order
            var (user1, user2) = message.SenderId < message.ReceiverId
                ? (message.SenderId, message.ReceiverId)
                : (message.ReceiverId, message.SenderId);

            // 1. Insert into messages_by_conversation (primary table)
            var conversationInsert = new SimpleStatement(
                @"INSERT INTO chat_logs.messages_by_conversation 
                  (company_id, user_id_1, user_id_2, message_timestamp, message_id, sender_id, content, created_at)
                  VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                  USING TTL 2592000"); // 30 days

            batch.Add(conversationInsert.Bind(
                message.CompanyId,
                user1,
                user2,
                message.MessageTimestamp,
                message.Id,
                message.SenderId,
                message.Content,
                DateTimeOffset.UtcNow.DateTime));

            // 2. Insert into messages_by_sender (index table)
            var senderInsert = new SimpleStatement(
                @"INSERT INTO chat_logs.messages_by_sender
                  (company_id, sender_id, message_timestamp, message_id, receiver_id, content, created_at)
                  VALUES (?, ?, ?, ?, ?, ?, ?)
                  USING TTL 2592000");

            batch.Add(senderInsert.Bind(
                message.CompanyId,
                message.SenderId,
                message.MessageTimestamp,
                message.Id,
                message.ReceiverId,
                message.Content,
                DateTimeOffset.UtcNow.DateTime));

            // 3. Insert into unread_messages (index table for notifications)
            var unreadInsert = new SimpleStatement(
                @"INSERT INTO chat_logs.unread_messages
                  (company_id, receiver_id, sender_id, message_timestamp, message_id, content, created_at)
                  VALUES (?, ?, ?, ?, ?, ?, ?)
                  USING TTL 2592000");

            batch.Add(unreadInsert.Bind(
                message.CompanyId,
                message.ReceiverId,
                message.SenderId,
                message.MessageTimestamp,
                message.Id,
                message.Content,
                DateTimeOffset.UtcNow.DateTime));

            // Execute batch atomically
            await session.ExecuteAsync(batch);

            _logger.LogInformation(
                "Message saved: MessageId={MessageId}, From={SenderId}, To={ReceiverId}",
                message.Id, message.SenderId, message.ReceiverId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving message to Cassandra");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IList<ChatMessage>> GetConversationAsync(
        Guid companyId,
        Guid userId1,
        Guid userId2,
        int pageSize = 50,
        long? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await _sessionFactory.GetSessionAsync();

            // Query messages_by_conversation
            var query = @"SELECT * FROM chat_logs.messages_by_conversation
                        WHERE company_id = ? AND user_id_1 = ? AND user_id_2 = ?
                        ORDER BY message_timestamp DESC";

            if (pageToken.HasValue)
            {
                query += " AND message_timestamp < ?";
            }

            query += $" LIMIT {pageSize}";

            var statement = new SimpleStatement(query);
            
            if (pageToken.HasValue)
            {
                statement = statement.Bind(companyId, userId1, userId2, pageToken.Value);
            }
            else
            {
                statement = statement.Bind(companyId, userId1, userId2);
            }

            statement.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);
            statement.SetPageSize(pageSize);

            var result = await session.ExecuteAsync(statement);

            var messages = result.Select(row => MapRowToMessage(row)).ToList();

            _logger.LogInformation(
                "Retrieved conversation: Company={CompanyId}, User1={UserId1}, User2={UserId2}, Count={Count}",
                companyId, userId1, userId2, messages.Count);

            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation from Cassandra");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> GetUnreadCountAsync(
        Guid companyId,
        Guid receiverId,
        Guid? senderId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await _sessionFactory.GetSessionAsync();

            string query;
            SimpleStatement statement;

            if (senderId.HasValue)
            {
                // Count unread from specific sender
                query = @"SELECT COUNT(*) as count FROM chat_logs.unread_messages
                         WHERE company_id = ? AND receiver_id = ? AND sender_id = ?";
                statement = new SimpleStatement(query).Bind(companyId, receiverId, senderId.Value);
            }
            else
            {
                // Count total unread (all senders)
                query = @"SELECT COUNT(*) as count FROM chat_logs.unread_messages
                         WHERE company_id = ? AND receiver_id = ?";
                statement = new SimpleStatement(query).Bind(companyId, receiverId);
            }

            statement.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);

            var result = await session.ExecuteAsync(statement);
            var row = result.FirstOrDefault();

            return row != null ? row.GetValue<long>("count").GetHashCode() : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count from Cassandra");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(
        Guid companyId,
        Guid receiverId,
        Guid senderId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await _sessionFactory.GetSessionAsync();

            // Delete all unread entries for this sender-receiver pair
            var query = @"DELETE FROM chat_logs.unread_messages
                        WHERE company_id = ? AND receiver_id = ? AND sender_id = ?";

            var statement = new SimpleStatement(query).Bind(companyId, receiverId, senderId);
            statement.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);

            await session.ExecuteAsync(statement);

            _logger.LogInformation(
                "Marked as read: Company={CompanyId}, Receiver={ReceiverId}, Sender={SenderId}",
                companyId, receiverId, senderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking messages as read");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IList<ChatMessage>> GetSentMessagesAsync(
        Guid companyId,
        Guid senderId,
        int pageSize = 50,
        long? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await _sessionFactory.GetSessionAsync();

            var query = @"SELECT * FROM chat_logs.messages_by_sender
                        WHERE company_id = ? AND sender_id = ?
                        ORDER BY message_timestamp DESC";

            if (pageToken.HasValue)
            {
                query += " AND message_timestamp < ?";
            }

            query += $" LIMIT {pageSize}";

            var statement = new SimpleStatement(query);

            if (pageToken.HasValue)
            {
                statement = statement.Bind(companyId, senderId, pageToken.Value);
            }
            else
            {
                statement = statement.Bind(companyId, senderId);
            }

            statement.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);
            statement.SetPageSize(pageSize);

            var result = await session.ExecuteAsync(statement);
            var messages = result.Select(row => MapSenderRowToMessage(row)).ToList();

            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sent messages from Cassandra");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteMessageAsync(
        Guid companyId,
        Guid messageId,
        Guid userId1,
        Guid userId2,
        long messageTimestamp,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await _sessionFactory.GetSessionAsync();

            // Soft delete: delete from conversation table
            var query = @"DELETE FROM chat_logs.messages_by_conversation
                        WHERE company_id = ? AND user_id_1 = ? AND user_id_2 = ? 
                        AND message_timestamp = ? AND message_id = ?";

            var statement = new SimpleStatement(query).Bind(companyId, userId1, userId2, messageTimestamp, messageId);
            statement.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);

            await session.ExecuteAsync(statement);

            _logger.LogInformation(
                "Message deleted: MessageId={MessageId}, Company={CompanyId}",
                messageId, companyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message from Cassandra");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IList<ChatMessage>> GetUnreadMessagesAsync(
        Guid companyId,
        Guid receiverId,
        int pageSize = 50,
        long? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await _sessionFactory.GetSessionAsync();

            var query = @"SELECT * FROM chat_logs.unread_messages
                        WHERE company_id = ? AND receiver_id = ?
                        ORDER BY message_timestamp DESC";

            if (pageToken.HasValue)
            {
                query += " AND message_timestamp < ?";
            }

            query += $" LIMIT {pageSize}";

            var statement = new SimpleStatement(query);

            if (pageToken.HasValue)
            {
                statement = statement.Bind(companyId, receiverId, pageToken.Value);
            }
            else
            {
                statement = statement.Bind(companyId, receiverId);
            }

            statement.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);
            statement.SetPageSize(pageSize);

            var result = await session.ExecuteAsync(statement);
            var messages = result.Select(row => MapUnreadRowToMessage(row)).ToList();

            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unread messages from Cassandra");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<Guid, int>> GetUnreadBySenderAsync(
        Guid companyId,
        Guid receiverId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await _sessionFactory.GetSessionAsync();

            var query = @"SELECT sender_id, COUNT(*) as count FROM chat_logs.unread_messages
                        WHERE company_id = ? AND receiver_id = ?
                        GROUP BY sender_id";

            var statement = new SimpleStatement(query).Bind(companyId, receiverId);
            statement.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);

            var result = await session.ExecuteAsync(statement);

            var unreadBySender = new Dictionary<Guid, int>();
            foreach (var row in result)
            {
                var senderId = row.GetValue<Guid>("sender_id");
                var count = (int)row.GetValue<long>("count");
                unreadBySender[senderId] = count;
            }

            return unreadBySender;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unread messages by sender from Cassandra");
            throw;
        }
    }

    private static ChatMessage MapRowToMessage(Row row)
    {
        return ChatMessage.CreateFromDatabase(
            row.GetValue<Guid>("message_id"),
            row.GetValue<Guid>("company_id"),
            row.GetValue<Guid>("sender_id"),
            row.GetValue<Guid?>("receiver_id") ?? Guid.Empty,
            row.GetValue<string>("content") ?? string.Empty,
            row.GetValue<long>("message_timestamp"),
            false,
            null,
            false,
            DateTime.UtcNow);
    }

    private static ChatMessage MapSenderRowToMessage(Row row)
    {
        return ChatMessage.CreateFromDatabase(
            row.GetValue<Guid>("message_id"),
            row.GetValue<Guid>("company_id"),
            row.GetValue<Guid>("sender_id"),
            row.GetValue<Guid?>("receiver_id") ?? Guid.Empty,
            row.GetValue<string>("content") ?? string.Empty,
            row.GetValue<long>("message_timestamp"),
            false,
            null,
            false,
            DateTime.UtcNow);
    }

    private static ChatMessage MapUnreadRowToMessage(Row row)
    {
        return ChatMessage.CreateFromDatabase(
            row.GetValue<Guid>("message_id"),
            row.GetValue<Guid>("company_id"),
            row.GetValue<Guid>("sender_id"),
            row.GetValue<Guid?>("receiver_id") ?? Guid.Empty,
            row.GetValue<string>("content") ?? string.Empty,
            row.GetValue<long>("message_timestamp"),
            false,
            null,
            false,
            DateTime.UtcNow);
    }
}

