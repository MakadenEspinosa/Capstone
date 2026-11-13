namespace ESEvents.Application.Chat;

/// <summary>
/// Application service for chat business logic.
/// Orchestrates between domain entities, repository, and external services.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Sends a message from one user to another.
    /// Validates permissions, creates domain entity, persists to repository.
    /// </summary>
    /// <param name="companyId">Company ID for multi-tenancy</param>
    /// <param name="senderId">Current user sending the message</param>
    /// <param name="receiverId">User receiving the message</param>
    /// <param name="content">Message content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>DTO of created message</returns>
    /// <exception cref="ArgumentException">If content invalid or users same company</exception>
    Task<ChatMessageDto> SendMessageAsync(
        Guid companyId,
        Guid senderId,
        Guid receiverId,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves conversation history between two users.
    /// Returns newest messages first, handles pagination.
    /// </summary>
    /// <param name="companyId">Company ID</param>
    /// <param name="userId">Current user</param>
    /// <param name="otherUserId">User to chat with</param>
    /// <param name="pageSize">Messages per page</param>
    /// <param name="pageToken">Pagination token (timestamp)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<GetConversationResponse> GetConversationAsync(
        Guid companyId,
        Guid userId,
        Guid otherUserId,
        int pageSize = 50,
        long? pageToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unread message count for the current user.
    /// Optionally filtered by a specific sender.
    /// </summary>
    /// <param name="companyId">Company ID</param>
    /// <param name="userId">User checking unread count</param>
    /// <param name="fromUserId">Optional: count unread from specific user only</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<int> GetUnreadCountAsync(
        Guid companyId,
        Guid userId,
        Guid? fromUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all messages from a sender as read for the receiver.
    /// Removes entries from unread_messages table.
    /// </summary>
    /// <param name="companyId">Company ID</param>
    /// <param name="userId">User marking messages as read</param>
    /// <param name="fromUserId">User whose messages are being read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkAsReadAsync(
        Guid companyId,
        Guid userId,
        Guid fromUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets list of recent conversations for a user.
    /// Sorted by last message timestamp descending.
    /// Optionally includes archived conversations.
    /// </summary>
    /// <param name="companyId">Company ID</param>
    /// <param name="userId">User requesting conversations</param>
    /// <param name="includeArchived">Include archived conversations</param>
    /// <param name="pageSize">Results per page</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<ConversationListResponse> GetConversationsAsync(
        Guid companyId,
        Guid userId,
        bool includeArchived = false,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unread message summary for a user.
    /// Includes total count and breakdown by sender.
    /// </summary>
    /// <param name="companyId">Company ID</param>
    /// <param name="userId">User requesting summary</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<UnreadSummary> GetUnreadSummaryAsync(
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a message (soft delete).
    /// Message remains in DB but marked as deleted.
    /// </summary>
    /// <param name="companyId">Company ID</param>
    /// <param name="userId">User deleting the message (must be sender or receiver)</param>
    /// <param name="messageId">Message to delete</param>
    /// <param name="otherUserId">Other participant in conversation</param>
    /// <param name="messageTimestamp">Timestamp of message (for Cassandra query)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteMessageAsync(
        Guid companyId,
        Guid userId,
        Guid messageId,
        Guid otherUserId,
        long messageTimestamp,
        CancellationToken cancellationToken = default);
}
