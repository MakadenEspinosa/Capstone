namespace ESEvents.Application.Chat;

/// <summary>
/// Represents a single chat message in API responses.
/// </summary>
public class ChatMessageDto
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public Guid ReceiverId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>
/// Request to send a new message.
/// </summary>
public class SendMessageRequest
{
    public Guid ReceiverId { get; set; }
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Response after sending a message successfully.
/// </summary>
public class SendMessageResponse
{
    public Guid MessageId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string Message { get; set; } = "Message sent successfully";
}

/// <summary>
/// Represents a conversation in the UI/API.
/// </summary>
public class ConversationDto
{
    public Guid Id { get; set; }
    public Guid OtherUserId { get; set; }
    public string OtherUserName { get; set; } = string.Empty;
    public string? OtherUserAvatarUrl { get; set; }
    public DateTime? LastMessageAtUtc { get; set; }
    public string? LastMessagePreview { get; set; }
    public int UnreadCount { get; set; }
    public bool IsArchived { get; set; }
}

/// <summary>
/// <summary>
/// Request to get conversation history.
/// </summary>
public class GetConversationRequest
{
    public Guid OtherUserId { get; set; }
    public int PageSize { get; set; } = 50;
    public int? PageNumber { get; set; } = 1;
}

/// <summary>
/// Response with conversation history.
/// </summary>
public class GetConversationResponse
{
    public List<ChatMessageDto> Messages { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public bool HasNextPage { get; set; }
}

/// <summary>
/// Request to mark messages as read.
/// </summary>
public class MarkAsReadRequest
{
    public Guid OtherUserId { get; set; }
}

/// <summary>
/// Request to archive a conversation.
/// </summary>
public class ArchiveConversationRequest
{
    public Guid OtherUserId { get; set; }
}

/// <summary>
/// List of conversations with metadata.
/// </summary>
public class ConversationListResponse
{
    public List<ConversationDto> Conversations { get; set; } = new();
    public int TotalCount { get; set; }
    public bool IncludeArchived { get; set; }
}

/// <summary>
/// Unread messages summary.
/// </summary>
public class UnreadSummary
{
    public int TotalUnreadCount { get; set; }
    public Dictionary<Guid, int> UnreadByUser { get; set; } = new();
}
