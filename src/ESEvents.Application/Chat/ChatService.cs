using ESEvents.Application.Companies;
using ESEvents.Domain.Chat;
using Microsoft.Extensions.Logging;

namespace ESEvents.Application.Chat;

public sealed class ChatService(
    IChatRepository messages,
    IChatConversationRepository conversations,
    ICompanyService companies,
    IChatNotifier notifier,
    ILogger<ChatService> logger) : IChatService
{
    public async Task<CreateConversationResponse> CreateConversationAsync(
        Guid companyId, Guid userId, Guid otherUserId, CancellationToken cancellationToken = default)
    {
        await EnsureParticipantsAsync(companyId, userId, otherUserId, cancellationToken);
        var (user1, user2) = Normalize(userId, otherUserId);
        var existing = await conversations.GetAsync(companyId, user1, user2, cancellationToken);
        var conversation = existing ??
            await conversations.GetOrCreateAsync(companyId, user1, user2, cancellationToken);
        conversation.Unarchive(userId);
        await conversations.SaveChangesAsync(cancellationToken);
        var dto = await ToConversationDtoAsync(conversation, userId, cancellationToken);
        return new CreateConversationResponse(dto, existing is null);
    }

    public Task<IReadOnlyList<CompanyMemberDto>> GetCompanyMembersAsync(
        Guid companyId, Guid userId, CancellationToken cancellationToken = default) =>
        companies.GetMembersAsync(companyId, userId, cancellationToken);

    public async Task<ChatMessageDto> SendMessageAsync(
        Guid companyId,
        Guid senderId,
        Guid receiverId,
        string content,
        CancellationToken cancellationToken = default)
    {
        await EnsureParticipantsAsync(companyId, senderId, receiverId, cancellationToken);
        var (user1, user2) = Normalize(senderId, receiverId);
        var conversation = await conversations.GetOrCreateAsync(companyId, user1, user2, cancellationToken);

        var message = ChatMessage.Create(companyId, senderId, receiverId, content);
        await messages.SaveMessageAsync(message, cancellationToken);

        conversation.Unarchive(senderId);
        conversation.Unarchive(receiverId);
        conversation.AddMessage(message.MessageTimestamp, senderId);
        await conversations.SaveChangesAsync(cancellationToken);

        var dto = ToMessageDto(message, conversation.Id);
        var unreadCount = await messages.GetUnreadCountAsync(companyId, receiverId, senderId, cancellationToken);
        await notifier.MessageSentAsync(dto, unreadCount, cancellationToken);

        logger.LogInformation(
            "Chat message {MessageId} persisted for company {CompanyId}",
            message.Id, companyId);
        return dto;
    }

    public async Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(
        Guid companyId,
        Guid userId,
        bool includeArchived = false,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        await EnsureMemberAsync(companyId, userId, cancellationToken);
        ValidatePage(pageSize, pageNumber);

        var items = await conversations.GetForUserAsync(
            companyId, userId, includeArchived, pageSize, pageNumber, cancellationToken);
        var result = new List<ConversationDto>(items.Count);
        foreach (var item in items)
            result.Add(await ToConversationDtoAsync(item, userId, cancellationToken));

        return result;
    }

    public async Task<GetConversationResponse> GetConversationAsync(
        Guid companyId,
        Guid userId,
        Guid otherUserId,
        int pageSize = 50,
        long? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureParticipantsAsync(companyId, userId, otherUserId, cancellationToken);
        if (pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 100.");

        var (user1, user2) = Normalize(userId, otherUserId);
        var conversation = await conversations.GetAsync(companyId, user1, user2, cancellationToken)
            ?? throw new KeyNotFoundException("Conversation was not found.");

        var found = await messages.GetConversationAsync(
            companyId, user1, user2, pageSize + 1, pageToken, cancellationToken);
        var hasNextPage = found.Count > pageSize;
        var page = found.Take(pageSize).ToArray();

        return new GetConversationResponse
        {
            Messages = page.Select(message => ToMessageDto(message, conversation.Id)).ToList(),
            TotalCount = page.Length,
            PageNumber = 1,
            PageSize = pageSize,
            HasNextPage = hasNextPage,
            NextPageToken = hasNextPage && page.Length > 0 ? page[^1].MessageTimestamp : null
        };
    }

    public async Task<int> GetUnreadCountAsync(
        Guid companyId, Guid userId, Guid? fromUserId = null, CancellationToken cancellationToken = default)
    {
        await EnsureMemberAsync(companyId, userId, cancellationToken);
        if (fromUserId.HasValue)
            await EnsureParticipantsAsync(companyId, userId, fromUserId.Value, cancellationToken);

        return await messages.GetUnreadCountAsync(companyId, userId, fromUserId, cancellationToken);
    }

    public async Task MarkAsReadAsync(
        Guid companyId, Guid userId, Guid fromUserId, CancellationToken cancellationToken = default)
    {
        await EnsureParticipantsAsync(companyId, userId, fromUserId, cancellationToken);
        var (user1, user2) = Normalize(userId, fromUserId);
        var conversation = await conversations.GetAsync(companyId, user1, user2, cancellationToken)
            ?? throw new KeyNotFoundException("Conversation was not found.");

        await messages.MarkAsReadAsync(companyId, userId, fromUserId, cancellationToken);
        conversation.MarkAsRead(userId);
        await conversations.SaveChangesAsync(cancellationToken);
        await notifier.MessagesReadAsync(companyId, userId, fromUserId, cancellationToken);
    }

    public async Task<UnreadSummary> GetUnreadSummaryAsync(
        Guid companyId, Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsureMemberAsync(companyId, userId, cancellationToken);
        var unreadByUser = await messages.GetUnreadBySenderAsync(companyId, userId, cancellationToken);
        return new UnreadSummary
        {
            TotalUnreadCount = unreadByUser.Values.Sum(),
            UnreadByUser = unreadByUser
        };
    }

    public async Task DeleteMessageAsync(
        Guid companyId,
        Guid userId,
        Guid messageId,
        Guid otherUserId,
        long messageTimestamp,
        CancellationToken cancellationToken = default)
    {
        await EnsureParticipantsAsync(companyId, userId, otherUserId, cancellationToken);
        var (user1, user2) = Normalize(userId, otherUserId);
        await messages.DeleteMessageAsync(
            companyId, messageId, user1, user2, messageTimestamp, cancellationToken);
    }

    private async Task EnsureParticipantsAsync(
        Guid companyId, Guid userId, Guid otherUserId, CancellationToken ct)
    {
        if (userId == otherUserId)
            throw new ArgumentException("Cannot start a conversation with yourself.", nameof(otherUserId));
        if (companyId == Guid.Empty || userId == Guid.Empty || otherUserId == Guid.Empty)
            throw new ArgumentException("Company and user IDs are required.");

        await EnsureMemberAsync(companyId, userId, ct);
        await EnsureMemberAsync(companyId, otherUserId, ct);
    }

    private async Task EnsureMemberAsync(Guid companyId, Guid userId, CancellationToken ct)
    {
        if (!await companies.IsMemberAsync(companyId, userId, ct))
            throw new UnauthorizedAccessException("Chat is limited to active members of the same company.");
    }

    private async Task<ConversationDto> ToConversationDtoAsync(
        Conversation conversation, Guid userId, CancellationToken ct)
    {
        var otherUserId = conversation.UserId1 == userId ? conversation.UserId2 : conversation.UserId1;
        var members = await companies.GetMembersAsync(conversation.CompanyId, userId, ct);
        var otherUser = members.FirstOrDefault(member => member.UserId == otherUserId)
            ?? throw new UnauthorizedAccessException("Conversation participant is no longer a company member.");

        var lastMessage = conversation.LastMessageTimestamp.HasValue
            ? (await messages.GetConversationAsync(
                conversation.CompanyId, conversation.UserId1, conversation.UserId2, 1, null, ct))
                .FirstOrDefault()
            : null;
        var unreadCount = await messages.GetUnreadCountAsync(
            conversation.CompanyId, userId, otherUserId, ct);
        var timestamp = conversation.LastMessageTimestamp.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(conversation.LastMessageTimestamp.Value).UtcDateTime
            : conversation.CreatedAtUtc;

        return new ConversationDto
        {
            Id = conversation.Id,
            CompanyId = conversation.CompanyId,
            OtherUserId = otherUserId,
            OtherUserName = $"{otherUser.FirstName} {otherUser.LastName}".Trim(),
            LastMessageAtUtc = lastMessage?.CreatedAtUtc,
            LastMessageTimestamp = timestamp,
            LastMessagePreview = lastMessage?.IsDeleted == true ? "Message deleted" : lastMessage?.Content,
            UnreadCount = unreadCount,
            IsArchived = conversation.IsArchived(userId)
        };
    }

    private static ChatMessageDto ToMessageDto(ChatMessage message, Guid conversationId) => new()
    {
        Id = message.Id,
        ConversationId = conversationId,
        CompanyId = message.CompanyId,
        SenderId = message.SenderId,
        ReceiverId = message.ReceiverId,
        Content = message.Content,
        CreatedAtUtc = message.CreatedAtUtc,
        IsRead = message.IsRead,
        ReadAtUtc = message.ReadAtUtc,
        IsDeleted = message.IsDeleted
    };

    private static (Guid User1, Guid User2) Normalize(Guid userId1, Guid userId2) =>
        userId1 < userId2 ? (userId1, userId2) : (userId2, userId1);

    private static void ValidatePage(int pageSize, int pageNumber)
    {
        if (pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 100.");
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be positive.");
    }
}
