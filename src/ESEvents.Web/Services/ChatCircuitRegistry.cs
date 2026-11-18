using System.Collections.Concurrent;
using ESEvents.Application.Chat;

namespace ESEvents.Web.Services;

public sealed class ChatCircuitRegistry
{
    private sealed record Registration(
        Guid UserId,
        Guid CompanyId,
        Action<ChatMessageDto> MessageReceived,
        Action<string> MessagesRead,
        Action<string> UserOnline,
        Action<string> UserOffline,
        Action<string> UserTyping);

    private readonly ConcurrentDictionary<Guid, Registration> _registrations = new();

    public IDisposable Subscribe(
        Guid userId,
        Guid companyId,
        Action<ChatMessageDto> messageReceived,
        Action<string> messagesRead,
        Action<string> userOnline,
        Action<string> userOffline,
        Action<string> userTyping)
    {
        var id = Guid.NewGuid();
        var registration = new Registration(
            userId, companyId, messageReceived, messagesRead, userOnline, userOffline, userTyping);
        var isFirstConnection = !_registrations.Values.Any(item =>
            item.CompanyId == companyId && item.UserId == userId);
        _registrations[id] = registration;

        if (isFirstConnection)
            PublishPresence(companyId, userId, online: true);

        return new Subscription(this, id);
    }

    public void PublishMessage(ChatMessageDto message)
    {
        foreach (var registration in _registrations.Values.Where(item =>
                     item.CompanyId == message.CompanyId &&
                     (item.UserId == message.SenderId || item.UserId == message.ReceiverId)))
            registration.MessageReceived(message);
    }

    public void PublishTyping(Guid companyId, Guid senderId, Guid recipientId)
    {
        foreach (var registration in _registrations.Values.Where(item =>
                     item.CompanyId == companyId && item.UserId == recipientId))
            registration.UserTyping(senderId.ToString());
    }

    public void PublishMessagesRead(Guid companyId, Guid readerId, Guid senderId)
    {
        foreach (var registration in _registrations.Values.Where(item =>
                     item.CompanyId == companyId &&
                     (item.UserId == readerId || item.UserId == senderId)))
            registration.MessagesRead(readerId.ToString());
    }

    public IReadOnlyList<Guid> GetOnlineUsers(Guid companyId) =>
        _registrations.Values.Where(item => item.CompanyId == companyId)
            .Select(item => item.UserId)
            .Distinct()
            .ToArray();

    private void Unsubscribe(Guid registrationId)
    {
        if (!_registrations.TryRemove(registrationId, out var registration)) return;
        if (!_registrations.Values.Any(item =>
                item.CompanyId == registration.CompanyId && item.UserId == registration.UserId))
            PublishPresence(registration.CompanyId, registration.UserId, online: false);
    }

    private void PublishPresence(Guid companyId, Guid userId, bool online)
    {
        foreach (var registration in _registrations.Values.Where(item => item.CompanyId == companyId))
        {
            if (online)
                registration.UserOnline(userId.ToString());
            else
                registration.UserOffline(userId.ToString());
        }
    }

    private sealed class Subscription(ChatCircuitRegistry registry, Guid id) : IDisposable
    {
        public void Dispose() => registry.Unsubscribe(id);
    }
}
