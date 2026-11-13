using ESEvents.Domain.Chat;

namespace ESEvents.Application.Chat;

/// <summary>
/// Repository interface for chat message persistence.
/// Abstracts Cassandra implementation details from application layer.
/// </summary>
public interface IChatRepository
{
    /// <summary>
    /// Saves a new message to all three Cassandra tables atomically.
    /// Ensures consistency across denormalized tables.
    /// </summary>
    /// <param name="message">The message to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveMessageAsync(ChatMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves conversation history between two users with pagination.
    /// Queries messages_by_conversation table.
    /// </summary>
    /// <param name="companyId">Company ID for data isolation</param>
    /// <param name="userId1">First participant</param>
    /// <param name="userId2">Second participant</param>
    /// <param name="pageSize">Number of messages per page</param>
    /// <param name="pageToken">Pagination token (timestamp of last message)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of messages in descending timestamp order (newest first)</returns>
    Task<IList<ChatMessage>> GetConversationAsync(
        Guid companyId,
        Guid userId1,
        Guid userId2,
        int pageSize = 50,
        long? pageToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unread message count for a user from a specific sender.
    /// Queries unread_messages table.
    /// </summary>
    /// <param name="companyId">Company ID</param>
    /// <param name="receiverId">User receiving messages</param>
    /// <param name="senderId">User sending messages (optional - get all unread if null)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<int> GetUnreadCountAsync(
        Guid companyId,
        Guid receiverId,
        Guid? senderId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks messages as read by removing them from unread_messages table.
    /// Called after user reads a conversation.
    /// </summary>
    /// <param name="companyId">Company ID</param>
    /// <param name="receiverId">User who is reading</param>
    /// <param name="senderId">User whose messages are being marked as read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkAsReadAsync(
        Guid companyId,
        Guid receiverId,
        Guid senderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all messages sent by a user (for sent message archive).
    /// Queries messages_by_sender table.
    /// </summary>
    /// <param name="companyId">Company ID</param>
    /// <param name="senderId">User who sent the messages</param>
    /// <param name="pageSize">Number of messages per page</param>
    /// <param name="pageToken">Pagination token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<IList<ChatMessage>> GetSentMessagesAsync(
        Guid companyId,
        Guid senderId,
        int pageSize = 50,
        long? pageToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a message (marks as deleted).
    /// The message remains in database with TTL handling eventual deletion.
    /// </summary>
    /// <param name="companyId">Company ID</param>
    /// <param name="messageId">Message to delete</param>
    /// <param name="userId1">First participant</param>
    /// <param name="userId2">Second participant</param>
    /// <param name="messageTimestamp">Timestamp of the message (Cassandra clustering key)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteMessageAsync(
        Guid companyId,
        Guid messageId,
        Guid userId1,
        Guid userId2,
        long messageTimestamp,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unread messages for a user from all senders.
    /// Queries unread_messages table.
    /// </summary>
    /// <param name="companyId">Company ID</param>
    /// <param name="receiverId">User receiving messages</param>
    /// <param name="pageSize">Number of messages per page</param>
    /// <param name="pageToken">Pagination token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<IList<ChatMessage>> GetUnreadMessagesAsync(
        Guid companyId,
        Guid receiverId,
        int pageSize = 50,
        long? pageToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unique senders of unread messages for a user.
    /// Used to build unread notification badges.
    /// </summary>
    /// <param name="companyId">Company ID</param>
    /// <param name="receiverId">User receiving messages</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Dictionary<Guid, int>> GetUnreadBySenderAsync(
        Guid companyId,
        Guid receiverId,
        CancellationToken cancellationToken = default);
}
