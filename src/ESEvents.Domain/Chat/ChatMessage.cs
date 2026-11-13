using ESEvents.Domain.Common;

namespace ESEvents.Domain.Chat;

/// <summary>
/// Domain entity representing a single chat message between two users.
/// Immutable once created - messages are not edited, only deleted (soft or via TTL).
/// </summary>
public class ChatMessage : Entity
{
    /// <summary>
    /// The company/organization this message belongs to.
    /// Ensures data isolation in multi-tenant system.
    /// </summary>
    public Guid CompanyId { get; private set; }

    /// <summary>
    /// The user who sent the message.
    /// </summary>
    public Guid SenderId { get; private set; }

    /// <summary>
    /// The user who receives the message.
    /// </summary>
    public Guid ReceiverId { get; private set; }

    /// <summary>
    /// The message content/body.
    /// Validated for length (max 5000 chars).
    /// </summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>
    /// Timestamp when message was sent (for sorting/pagination).
    /// Cassandra uses this for efficient range queries.
    /// </summary>
    public long MessageTimestamp { get; private set; }

    /// <summary>
    /// Whether this message has been read by the receiver.
    /// Soft flag - doesn't delete message, just tracks state.
    /// </summary>
    public bool IsRead { get; private set; }

    /// <summary>
    /// Timestamp when message was marked as read (if at all).
    /// </summary>
    public DateTime? ReadAtUtc { get; private set; }

    /// <summary>
    /// Soft delete flag - message stays in DB but marked as deleted.
    /// </summary>
    public bool IsDeleted { get; private set; }

    // Private ctor for EF/serialization
    // Contains setter logic to support deserialization from persistence layer
    private ChatMessage() { }

    /// <summary>
    /// Factory for repository deserialization.
    /// Only used by repository layer when loading from Cassandra.
    /// </summary>
    public static ChatMessage CreateFromDatabase(
        Guid id,
        Guid companyId,
        Guid senderId,
        Guid receiverId,
        string content,
        long messageTimestamp,
        bool isRead,
        DateTime? readAtUtc,
        bool isDeleted,
        DateTime createdAtUtc)
    {
        var message = new ChatMessage();
        
        // Use reflection to set read-only properties from persistence layer
        typeof(ChatMessage).GetProperty(nameof(Id))!.SetValue(message, id);
        typeof(ChatMessage).GetProperty(nameof(CompanyId))!.SetValue(message, companyId);
        typeof(ChatMessage).GetProperty(nameof(SenderId))!.SetValue(message, senderId);
        typeof(ChatMessage).GetProperty(nameof(ReceiverId))!.SetValue(message, receiverId);
        typeof(ChatMessage).GetProperty(nameof(Content))!.SetValue(message, content);
        typeof(ChatMessage).GetProperty(nameof(MessageTimestamp))!.SetValue(message, messageTimestamp);
        typeof(ChatMessage).GetProperty(nameof(IsRead))!.SetValue(message, isRead);
        typeof(ChatMessage).GetProperty(nameof(ReadAtUtc))!.SetValue(message, readAtUtc);
        typeof(ChatMessage).GetProperty(nameof(IsDeleted))!.SetValue(message, isDeleted);
        
        var baseType = typeof(Entity);
        baseType.GetProperty(nameof(CreatedAtUtc))!.SetValue(message, createdAtUtc);
        
        return message;
    }

    /// <summary>
    /// Creates a new outgoing chat message.
    /// </summary>
    /// <param name="companyId">Company/organization ID</param>
    /// <param name="senderId">User sending the message</param>
    /// <param name="receiverId">User receiving the message</param>
    /// <param name="content">Message content</param>
    /// <exception cref="ArgumentException">Thrown when validation fails</exception>
    public static ChatMessage Create(Guid companyId, Guid senderId, Guid receiverId, string content)
    {
        ValidateInput(companyId, senderId, receiverId, content);

        var message = new ChatMessage
        {
            CompanyId = companyId,
            SenderId = senderId,
            ReceiverId = receiverId,
            Content = content.Trim(),
            MessageTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            IsRead = false,
            ReadAtUtc = null,
            IsDeleted = false
        };

        return message;
    }

    /// <summary>
    /// Marks this message as read by the receiver.
    /// </summary>
    public void MarkAsRead()
    {
        if (IsRead)
            return;

        IsRead = true;
        ReadAtUtc = DateTime.UtcNow;
        Touch();
    }

    /// <summary>
    /// Soft-deletes this message (marks as deleted without removing from DB).
    /// TTL will eventually purge it from Cassandra.
    /// </summary>
    public void Delete()
    {
        if (IsDeleted)
            return;

        IsDeleted = true;
        Touch();
    }

    private static void ValidateInput(Guid companyId, Guid senderId, Guid receiverId, string content)
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("Company ID cannot be empty", nameof(companyId));

        if (senderId == Guid.Empty)
            throw new ArgumentException("Sender ID cannot be empty", nameof(senderId));

        if (receiverId == Guid.Empty)
            throw new ArgumentException("Receiver ID cannot be empty", nameof(receiverId));

        if (senderId == receiverId)
            throw new ArgumentException("Cannot send message to yourself", nameof(receiverId));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Message content cannot be empty", nameof(content));

        if (content.Length > 5000)
            throw new ArgumentException("Message content cannot exceed 5000 characters", nameof(content));
    }
}
