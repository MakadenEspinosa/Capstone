using ESEvents.Application.Chat;

namespace ESEvents.Web.Services;

/// <summary>
/// Interface for HTTP REST API communication with the chat service
/// </summary>
public interface IChatApiService
{
    // Conversations
    Task<List<ConversationDto>> GetConversationsAsync();
    Task<ConversationDto> GetConversationAsync(Guid otherUserId, int pageSize = 50, long? pageToken = null);
    
    // Messages
    Task<GetConversationResponse> GetConversationAsync(
        Guid companyId, Guid userId1, Guid userId2, int pageSize = 50, long? pageToken = null);
    
    Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request);
    
    Task<MarkAsReadResponse> MarkAsReadAsync(Guid companyId, Guid userId1, Guid userId2);
    
    Task<DeleteMessageResponse> DeleteMessageAsync(Guid messageId, Guid companyId);
    
    // Unread
    Task<GetUnreadCountResponse> GetUnreadCountAsync(Guid companyId, Guid userId);
    
    Task<GetUnreadSummaryResponse> GetUnreadSummaryAsync(Guid companyId, Guid userId);
}

// DTOs for API responses
public class GetConversationResponse
{
    public List<ChatMessageDto> Messages { get; set; } = new();
    public long? NextPageToken { get; set; }
    public int TotalCount { get; set; }
}

public class SendMessageResponse
{
    public Guid MessageId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MarkAsReadResponse
{
    public int MessagesMarked { get; set; }
}

public class DeleteMessageResponse
{
    public bool Success { get; set; }
}

public class GetUnreadCountResponse
{
    public int TotalUnread { get; set; }
}

public class GetUnreadSummaryResponse
{
    public List<UnreadConversationDto> Conversations { get; set; } = new();
}

public class UnreadConversationDto
{
    public Guid OtherUserId { get; set; }
    public int UnreadCount { get; set; }
}
