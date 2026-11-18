using ESEvents.Application.Chat;

namespace ESEvents.Web.Services;

public interface ISignalRService
{
    bool IsConnected { get; }
    event Action<bool> OnConnectionStateChanged;
    event Action<ChatMessageDto> OnMessageReceived;
    event Action<string> OnMessagesRead;
    event Action<string> OnUserOnline;
    event Action<string> OnUserOffline;
    event Action<string> OnUserTyping;
    event Action<ReadReceiptDto> OnMessageRead;

    Task ConnectAsync(Guid userId, Guid companyId);
    Task DisconnectAsync();
    Task ReconnectAsync(Guid userId, Guid companyId);
    Task SendMessageAsync(Guid companyId, Guid receiverId, string content);
    Task MarkAsReadAsync(Guid companyId, Guid senderId);
    Task BroadcastTypingAsync(Guid companyId, Guid receiverId);
    Task<List<Guid>> GetOnlineUsersAsync(Guid companyId);
    Task<bool> IsUserOnlineAsync(Guid companyId, Guid userId);
    Task<Dictionary<Guid, bool>> GetUsersOnlineStatusAsync(Guid companyId, List<Guid> userIds);
}

public sealed class ReadReceiptDto
{
    public Guid MessageId { get; set; }
    public Guid ReadBy { get; set; }
    public DateTime ReadAt { get; set; }
}
