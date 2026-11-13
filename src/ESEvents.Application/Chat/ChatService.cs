using ESEvents.Application.Abstractions;
using ESEvents.Domain.Chat;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ESEvents.Application.Chat;

/// <summary>
/// Application service implementing chat business logic.
/// Coordinates between domain, repository, and notification systems.
/// </summary>
public class ChatService : IChatService
{
    private readonly IChatRepository _repository;
    private readonly ILogger<ChatService> _logger;

    public ChatService(IChatRepository repository, ILogger<ChatService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ChatMessageDto> SendMessageAsync(
        Guid companyId,
        Guid senderId,
        Guid receiverId,
        string content,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Send message: Company={CompanyId}, From={SenderId}, To={ReceiverId}, Length={ContentLength}",
            companyId, senderId, receiverId, content?.Length ?? 0);

        try
        {
            // Create domain entity (validates input)
            var message = ChatMessage.Create(companyId, senderId, receiverId, content ?? string.Empty);

            // Persist to repository (writes to all 3 Cassandra tables)
            await _repository.SaveMessageAsync(message, cancellationToken);

            _logger.LogInformation("Message sent successfully: {MessageId}", message.Id);

            // Return DTO
            return new ChatMessageDto
            {
                Id = message.Id,
                SenderId = message.SenderId,
                ReceiverId = message.ReceiverId,
                Content = message.Content,
                CreatedAtUtc = message.CreatedAtUtc,
                IsRead = message.IsRead,
                ReadAtUtc = message.ReadAtUtc,
                IsDeleted = message.IsDeleted
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Message validation failed");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<GetConversationResponse> GetConversationAsync(
        Guid companyId,
        Guid userId,
        Guid otherUserId,
        int pageSize = 50,
        long? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Get conversation: Company={CompanyId}, User={UserId}, Other={OtherUserId}, PageSize={PageSize}",
            companyId, userId, otherUserId, pageSize);

        // Normalize user order (same as domain)
        var (user1, user2) = userId < otherUserId
            ? (userId, otherUserId)
            : (otherUserId, userId);

        // Fetch messages from repository
        var messages = await _repository.GetConversationAsync(
            companyId, user1, user2, pageSize + 1, pageToken, cancellationToken);

        // Determine if there are more pages (we fetched pageSize + 1)
        var hasNextPage = messages.Count > pageSize;
        if (hasNextPage)
            messages = messages.Take(pageSize).ToList();

        // Get next page token (timestamp of last message if more pages)
        long? nextPageToken = hasNextPage && messages.Count > 0
            ? messages.Last().MessageTimestamp
            : null;

        var dtos = messages.Select(m => new ChatMessageDto
        {
            Id = m.Id,
            SenderId = m.SenderId,
            ReceiverId = m.ReceiverId,
            Content = m.Content,
            CreatedAtUtc = m.CreatedAtUtc,
            IsRead = m.IsRead,
            ReadAtUtc = m.ReadAtUtc,
            IsDeleted = m.IsDeleted
        }).ToList();

        return new GetConversationResponse
        {
            Messages = dtos,
            TotalCount = messages.Count,
            PageNumber = 1,
            PageSize = pageSize,
            HasNextPage = hasNextPage
        };
    }

    /// <inheritdoc />
    public async Task<int> GetUnreadCountAsync(
        Guid companyId,
        Guid userId,
        Guid? fromUserId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Get unread count: Company={CompanyId}, User={UserId}, From={FromUserId}",
            companyId, userId, fromUserId);

        return await _repository.GetUnreadCountAsync(companyId, userId, fromUserId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(
        Guid companyId,
        Guid userId,
        Guid fromUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Mark as read: Company={CompanyId}, User={UserId}, From={FromUserId}",
            companyId, userId, fromUserId);

        await _repository.MarkAsReadAsync(companyId, userId, fromUserId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConversationListResponse> GetConversationsAsync(
        Guid companyId,
        Guid userId,
        bool includeArchived = false,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Get conversations: Company={CompanyId}, User={UserId}, IncludeArchived={IncludeArchived}",
            companyId, userId, includeArchived);

        // TODO: This requires querying a separate conversation index table
        // For now, stub implementation - will be implemented in Phase 3
        // when we add the Conversation aggregate to database

        return new ConversationListResponse
        {
            Conversations = new(),
            TotalCount = 0,
            IncludeArchived = includeArchived
        };
    }

    /// <inheritdoc />
    public async Task<UnreadSummary> GetUnreadSummaryAsync(
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Get unread summary: Company={CompanyId}, User={UserId}",
            companyId, userId);

        // Get unread count by sender
        var unreadBySender = await _repository.GetUnreadBySenderAsync(companyId, userId, cancellationToken);
        var totalUnread = unreadBySender.Values.Sum();

        return new UnreadSummary
        {
            TotalUnreadCount = totalUnread,
            UnreadByUser = unreadBySender
        };
    }

    /// <inheritdoc />
    public async Task DeleteMessageAsync(
        Guid companyId,
        Guid userId,
        Guid messageId,
        Guid otherUserId,
        long messageTimestamp,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Delete message: Company={CompanyId}, User={UserId}, MessageId={MessageId}",
            companyId, userId, messageId);

        // Normalize user order
        var (user1, user2) = userId < otherUserId
            ? (userId, otherUserId)
            : (otherUserId, userId);

        // Delete from repository (soft delete)
        await _repository.DeleteMessageAsync(
            companyId, messageId, user1, user2, messageTimestamp, cancellationToken);

        _logger.LogInformation("Message deleted: {MessageId}", messageId);
    }
}
