using ESEvents.Application.Chat;
using ESEvents.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace ESEvents.Web.Hubs;

public sealed class SignalRChatNotifier(
    IHubContext<ChatHub> hub,
    ChatCircuitRegistry circuits) : IChatNotifier
{
    public async Task MessageSentAsync(
        ChatMessageDto message, int unreadCount, CancellationToken ct = default)
    {
        await hub.Clients.Group(ChatHub.UserGroup(message.ReceiverId))
            .SendAsync("MessageReceived", message, ct);
        await hub.Clients.Group(ChatHub.UserGroup(message.ReceiverId))
            .SendAsync("UnreadCountUpdated", unreadCount, ct);
        await hub.Clients.Group(ChatHub.UserGroup(message.SenderId))
            .SendAsync("MessageSent", message, ct);
        circuits.PublishMessage(message);
    }

    public async Task MessagesReadAsync(
        Guid companyId, Guid readerId, Guid senderId, CancellationToken ct = default)
    {
        await hub.Clients.Group(ChatHub.UserGroup(readerId))
            .SendAsync("MessagesMarkedRead", senderId, ct);
        await hub.Clients.Group(ChatHub.UserGroup(senderId))
            .SendAsync("YourMessagesRead", readerId, ct);
        circuits.PublishMessagesRead(companyId, readerId, senderId);
    }

    public async Task UserTypingAsync(Guid companyId, Guid senderId, Guid recipientId, CancellationToken ct = default)
    {
        await hub.Clients.Group(ChatHub.UserGroup(recipientId)).SendAsync("UserTyping", senderId, ct);
        circuits.PublishTyping(companyId, senderId, recipientId);
    }
}
