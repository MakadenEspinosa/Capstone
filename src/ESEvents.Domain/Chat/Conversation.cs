using ESEvents.Domain.Common;

namespace ESEvents.Domain.Chat;

/// <summary>
/// Aggregate root representing a conversation between two users.
/// Manages conversation state and participants.
/// </summary>
public class Conversation : Entity
{
    /// <summary>
    /// The company/organization this conversation belongs to.
    /// </summary>
    public Guid CompanyId { get; private set; }

    /// <summary>
    /// First participant (typically the one who initiated).
    /// Normalized to lower ID for consistent ordering.
    /// </summary>
    public Guid UserId1 { get; private set; }

    /// <summary>
    /// Second participant.
    /// Normalized to higher ID for consistent ordering.
    /// </summary>
    public Guid UserId2 { get; private set; }

    /// <summary>
    /// Last message timestamp in this conversation.
    /// Used for sorting conversations in "recent chats" view.
    /// </summary>
    public long? LastMessageTimestamp { get; private set; }

    /// <summary>
    /// Count of unread messages for UserId1 from UserId2.
    /// </summary>
    public int UnreadCountUser1 { get; private set; }

    /// <summary>
    /// Count of unread messages for UserId2 from UserId1.
    /// </summary>
    public int UnreadCountUser2 { get; private set; }

    /// <summary>
    /// Whether UserId1 has archived this conversation.
    /// </summary>
    public bool IsArchivedByUser1 { get; private set; }

    /// <summary>
    /// Whether UserId2 has archived this conversation.
    /// </summary>
    public bool IsArchivedByUser2 { get; private set; }

    // Private ctor for EF/serialization
    private Conversation() { }

    /// <summary>
    /// Creates a new conversation between two users.
    /// </summary>
    /// <param name="companyId">Company ID</param>
    /// <param name="userId1">First participant</param>
    /// <param name="userId2">Second participant</param>
    public static Conversation Create(Guid companyId, Guid userId1, Guid userId2)
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("Company ID cannot be empty", nameof(companyId));

        if (userId1 == Guid.Empty)
            throw new ArgumentException("User 1 ID cannot be empty", nameof(userId1));

        if (userId2 == Guid.Empty)
            throw new ArgumentException("User 2 ID cannot be empty", nameof(userId2));

        if (userId1 == userId2)
            throw new ArgumentException("Cannot have conversation with yourself", nameof(userId2));

        // Normalize participant order for consistent partition keys
        var (normalizedUser1, normalizedUser2) = userId1 < userId2
            ? (userId1, userId2)
            : (userId2, userId1);

        return new Conversation
        {
            CompanyId = companyId,
            UserId1 = normalizedUser1,
            UserId2 = normalizedUser2,
            LastMessageTimestamp = null,
            UnreadCountUser1 = 0,
            UnreadCountUser2 = 0,
            IsArchivedByUser1 = false,
            IsArchivedByUser2 = false
        };
    }

    /// <summary>
    /// Updates last message timestamp and updates unread count.
    /// Called when a new message is sent in this conversation.
    /// </summary>
    /// <param name="messageTimestamp">Timestamp of the new message</param>
    /// <param name="senderId">Who sent the message</param>
    public void AddMessage(long messageTimestamp, Guid senderId)
    {
        LastMessageTimestamp = messageTimestamp;

        if (senderId == UserId1)
        {
            UnreadCountUser2++;
        }
        else if (senderId == UserId2)
        {
            UnreadCountUser1++;
        }

        Touch();
    }

    /// <summary>
    /// Marks messages as read for a specific user.
    /// </summary>
    /// <param name="userId">The user reading the messages</param>
    public void MarkAsRead(Guid userId)
    {
        if (userId == UserId1)
        {
            UnreadCountUser1 = 0;
        }
        else if (userId == UserId2)
        {
            UnreadCountUser2 = 0;
        }

        Touch();
    }

    /// <summary>
    /// Archives this conversation for a specific user.
    /// </summary>
    /// <param name="userId">The user archiving the conversation</param>
    public void Archive(Guid userId)
    {
        if (userId == UserId1)
        {
            IsArchivedByUser1 = true;
        }
        else if (userId == UserId2)
        {
            IsArchivedByUser2 = true;
        }

        Touch();
    }

    /// <summary>
    /// Unarchives this conversation for a specific user.
    /// </summary>
    /// <param name="userId">The user unarchiving the conversation</param>
    public void Unarchive(Guid userId)
    {
        if (userId == UserId1)
        {
            IsArchivedByUser1 = false;
        }
        else if (userId == UserId2)
        {
            IsArchivedByUser2 = false;
        }

        Touch();
    }

    /// <summary>
    /// Gets the unread count for a specific user.
    /// </summary>
    public int GetUnreadCount(Guid userId) => userId == UserId1 ? UnreadCountUser1 : UnreadCountUser2;

    /// <summary>
    /// Checks if this conversation is archived for a specific user.
    /// </summary>
    public bool IsArchived(Guid userId) => userId == UserId1 ? IsArchivedByUser1 : IsArchivedByUser2;
}
