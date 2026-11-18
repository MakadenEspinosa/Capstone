using ESEvents.Application.Chat;
using ESEvents.Application.Companies;

namespace ESEvents.Web.Services;

public sealed class SignalRService(
    ChatCircuitRegistry circuits,
    IChatService chat,
    ICompanyService companies,
    IChatNotifier notifier,
    ILogger<SignalRService> logger) : ISignalRService, IAsyncDisposable
{
    private IDisposable? _subscription;
    private Guid _userId;
    private Guid _companyId;

    public bool IsConnected => _subscription is not null;
    public event Action<bool>? OnConnectionStateChanged;
    public event Action<ChatMessageDto>? OnMessageReceived;
    public event Action<string>? OnMessagesRead;
    public event Action<string>? OnUserOnline;
    public event Action<string>? OnUserOffline;
    public event Action<string>? OnUserTyping;
    public event Action<ReadReceiptDto>? OnMessageRead;

    public async Task ConnectAsync(Guid userId, Guid companyId)
    {
        if (!await companies.IsMemberAsync(companyId, userId))
            throw new UnauthorizedAccessException("You are not a member of this company.");

        if (IsConnected && _userId == userId && _companyId == companyId) return;
        await DisconnectAsync();
        _userId = userId;
        _companyId = companyId;
        _subscription = circuits.Subscribe(
            userId,
            companyId,
            message => OnMessageReceived?.Invoke(message),
            readerId => OnMessagesRead?.Invoke(readerId),
            userIdValue => OnUserOnline?.Invoke(userIdValue),
            userIdValue => OnUserOffline?.Invoke(userIdValue),
            userIdValue => OnUserTyping?.Invoke(userIdValue));
        OnConnectionStateChanged?.Invoke(true);
    }

    public Task DisconnectAsync()
    {
        if (_subscription is null) return Task.CompletedTask;
        _subscription.Dispose();
        _subscription = null;
        OnConnectionStateChanged?.Invoke(false);
        return Task.CompletedTask;
    }

    public async Task ReconnectAsync(Guid userId, Guid companyId)
    {
        await DisconnectAsync();
        await ConnectAsync(userId, companyId);
    }

    public async Task SendMessageAsync(Guid companyId, Guid receiverId, string content)
    {
        EnsureConnected(companyId);
        await chat.SendMessageAsync(companyId, _userId, receiverId, content);
    }

    public async Task MarkAsReadAsync(Guid companyId, Guid senderId)
    {
        EnsureConnected(companyId);
        await chat.MarkAsReadAsync(companyId, _userId, senderId);
    }

    public async Task BroadcastTypingAsync(Guid companyId, Guid receiverId)
    {
        EnsureConnected(companyId);
        if (!await companies.IsMemberAsync(companyId, receiverId))
            throw new UnauthorizedAccessException("Recipient is not a member of this company.");
        await notifier.UserTypingAsync(companyId, _userId, receiverId);
    }

    public async Task<List<Guid>> GetOnlineUsersAsync(Guid companyId)
    {
        EnsureConnected(companyId);
        return circuits.GetOnlineUsers(companyId).ToList();
    }

    public async Task<bool> IsUserOnlineAsync(Guid companyId, Guid userId)
    {
        EnsureConnected(companyId);
        if (!await companies.IsMemberAsync(companyId, userId))
            throw new UnauthorizedAccessException("Presence is limited to company members.");
        return circuits.GetOnlineUsers(companyId).Contains(userId);
    }

    public async Task<Dictionary<Guid, bool>> GetUsersOnlineStatusAsync(Guid companyId, List<Guid> userIds)
    {
        EnsureConnected(companyId);
        var members = await companies.GetMembersAsync(companyId, _userId);
        var allowedIds = members.Select(member => member.UserId).Append(_userId).ToHashSet();
        if (userIds.Any(userId => !allowedIds.Contains(userId)))
            throw new UnauthorizedAccessException("Presence is limited to company members.");

        var online = circuits.GetOnlineUsers(companyId).ToHashSet();
        return userIds.Distinct().ToDictionary(userId => userId, online.Contains);
    }

    private void EnsureConnected(Guid companyId)
    {
        if (!IsConnected) throw new InvalidOperationException("Chat connection is not active.");
        if (_companyId != companyId) throw new InvalidOperationException("Chat connection is for another company.");
    }

    public async ValueTask DisposeAsync()
    {
        logger.LogDebug("Disposing chat circuit for user {UserId}", _userId);
        await DisconnectAsync();
    }
}
