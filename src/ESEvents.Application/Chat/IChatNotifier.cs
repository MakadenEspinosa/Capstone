namespace ESEvents.Application.Chat;

public interface IChatNotifier
{
    Task MessageSentAsync(ChatMessageDto message, int unreadCount, CancellationToken ct = default);
    Task MessagesReadAsync(Guid companyId, Guid readerId, Guid senderId, CancellationToken ct = default);
    Task UserTypingAsync(Guid companyId, Guid senderId, Guid recipientId, CancellationToken ct = default);
}
