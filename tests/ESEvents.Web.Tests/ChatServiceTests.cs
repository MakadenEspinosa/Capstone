using ESEvents.Application.Chat;
using ESEvents.Application.Companies;
using ESEvents.Domain.Chat;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ESEvents.Web.Tests;

public sealed class ChatServiceTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _senderId = Guid.NewGuid();
    private readonly Guid _receiverId = Guid.NewGuid();
    private readonly FakeCompanyService _companies;
    private readonly FakeChatRepository _messages = new();
    private readonly FakeConversationRepository _conversations = new();
    private readonly FakeChatNotifier _notifier = new();
    private readonly ChatService _service;

    public ChatServiceTests()
    {
        _companies = new FakeCompanyService(_companyId, _senderId, _receiverId);
        _service = new ChatService(
            _messages,
            _conversations,
            _companies,
            _notifier,
            NullLogger<ChatService>.Instance);
    }

    [Fact]
    public async Task CreateConversation_IsIdempotentForACompanyUserPair()
    {
        var first = await _service.CreateConversationAsync(_companyId, _senderId, _receiverId);
        var second = await _service.CreateConversationAsync(_companyId, _receiverId, _senderId);

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.Conversation.Id, second.Conversation.Id);
        Assert.Equal(_receiverId, first.Conversation.OtherUserId);
        Assert.Single(_conversations.Store);
    }

    [Fact]
    public async Task SendMessage_PersistsThenNotifiesBothParticipants()
    {
        await _service.CreateConversationAsync(_companyId, _senderId, _receiverId);

        var sent = await _service.SendMessageAsync(
            _companyId, _senderId, _receiverId, "  Hello team  ");

        var stored = Assert.Single(_messages.Store);
        var notification = Assert.Single(_notifier.Sent);
        Assert.Equal(stored.Id, sent.Id);
        Assert.Equal("Hello team", sent.Content);
        Assert.Equal(_senderId, notification.Message.SenderId);
        Assert.Equal(_receiverId, notification.Message.ReceiverId);
        Assert.Equal(1, notification.UnreadCount);
    }

    [Fact]
    public async Task SendMessage_RejectsRecipientOutsideCompanyBeforeWriting()
    {
        var outsider = Guid.NewGuid();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.SendMessageAsync(_companyId, _senderId, outsider, "No access"));

        Assert.Empty(_messages.Store);
        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task MarkAsRead_RemovesUnreadAndNotifiesParticipants()
    {
        await _service.SendMessageAsync(_companyId, _senderId, _receiverId, "Hello");

        await _service.MarkAsReadAsync(_companyId, _receiverId, _senderId);

        Assert.Equal(0, await _service.GetUnreadCountAsync(_companyId, _receiverId));
        Assert.Equal((_companyId, _receiverId, _senderId), Assert.Single(_notifier.Read));
    }

    private sealed class FakeCompanyService(Guid companyId, params Guid[] members) : ICompanyService
    {
        public Task<CompanyDto> CreateAsync(string name, Guid creatorId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<CompanyDto?> GetAsync(Guid id, Guid requesterId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CompanyDto>> GetForUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CompanyDto>>([]);

        public async Task<IReadOnlyList<CompanyMemberDto>> GetMembersAsync(
            Guid id, Guid requesterId, CancellationToken ct = default)
        {
            if (!await IsMemberAsync(id, requesterId, ct))
                throw new UnauthorizedAccessException();
            return members.Where(member => member != requesterId)
                .Select((member, index) => new CompanyMemberDto(member, $"User{index}", "Test"))
                .ToArray();
        }

        public Task<CompanyDto?> AddMemberAsync(Guid id, Guid userId, Guid requesterId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<CompanyDto?> RemoveMemberAsync(Guid id, Guid userId, Guid requesterId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> GetCompanyIdsForUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(members.Contains(userId) ? [companyId] : []);

        public Task<bool> IsMemberAsync(Guid id, Guid userId, CancellationToken ct = default) =>
            Task.FromResult(id == companyId && members.Contains(userId));
    }

    private sealed class FakeChatRepository : IChatRepository
    {
        public List<ChatMessage> Store { get; } = [];

        public Task SaveMessageAsync(ChatMessage message, CancellationToken ct = default)
        {
            Store.Add(message);
            return Task.CompletedTask;
        }

        public Task<IList<ChatMessage>> GetConversationAsync(
            Guid companyId, Guid userId1, Guid userId2, int pageSize = 50, long? pageToken = null,
            CancellationToken ct = default)
        {
            var result = Store.Where(message =>
                    message.CompanyId == companyId &&
                    ((message.SenderId == userId1 && message.ReceiverId == userId2) ||
                     (message.SenderId == userId2 && message.ReceiverId == userId1)) &&
                    (!pageToken.HasValue || message.MessageTimestamp < pageToken))
                .OrderByDescending(message => message.MessageTimestamp)
                .Take(pageSize)
                .ToList();
            return Task.FromResult<IList<ChatMessage>>(result);
        }

        public Task<int> GetUnreadCountAsync(
            Guid companyId, Guid receiverId, Guid? senderId = null, CancellationToken ct = default) =>
            Task.FromResult(Store.Count(message =>
                message.CompanyId == companyId &&
                message.ReceiverId == receiverId &&
                !message.IsRead &&
                (!senderId.HasValue || message.SenderId == senderId)));

        public Task MarkAsReadAsync(
            Guid companyId, Guid receiverId, Guid senderId, CancellationToken ct = default)
        {
            foreach (var message in Store.Where(message =>
                         message.CompanyId == companyId &&
                         message.ReceiverId == receiverId &&
                         message.SenderId == senderId))
                message.MarkAsRead();
            return Task.CompletedTask;
        }

        public Task<IList<ChatMessage>> GetSentMessagesAsync(
            Guid companyId, Guid senderId, int pageSize = 50, long? pageToken = null,
            CancellationToken ct = default) => throw new NotSupportedException();

        public Task DeleteMessageAsync(
            Guid companyId, Guid messageId, Guid userId1, Guid userId2, long messageTimestamp,
            CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IList<ChatMessage>> GetUnreadMessagesAsync(
            Guid companyId, Guid receiverId, int pageSize = 50, long? pageToken = null,
            CancellationToken ct = default) => throw new NotSupportedException();

        public Task<Dictionary<Guid, int>> GetUnreadBySenderAsync(
            Guid companyId, Guid receiverId, CancellationToken ct = default) =>
            Task.FromResult(Store.Where(message =>
                    message.CompanyId == companyId && message.ReceiverId == receiverId && !message.IsRead)
                .GroupBy(message => message.SenderId)
                .ToDictionary(group => group.Key, group => group.Count()));
    }

    private sealed class FakeConversationRepository : IChatConversationRepository
    {
        public List<Conversation> Store { get; } = [];

        public Task<Conversation?> GetAsync(
            Guid companyId, Guid userId1, Guid userId2, CancellationToken ct = default) =>
            Task.FromResult(Store.FirstOrDefault(conversation =>
                conversation.CompanyId == companyId &&
                conversation.UserId1 == userId1 &&
                conversation.UserId2 == userId2));

        public async Task<Conversation> GetOrCreateAsync(
            Guid companyId, Guid userId1, Guid userId2, CancellationToken ct = default)
        {
            var existing = await GetAsync(companyId, userId1, userId2, ct);
            if (existing is not null) return existing;
            var created = Conversation.Create(companyId, userId1, userId2);
            Store.Add(created);
            return created;
        }

        public Task<IReadOnlyList<Conversation>> GetForUserAsync(
            Guid companyId, Guid userId, bool includeArchived, int pageSize, int pageNumber,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Conversation>>(Store.Where(conversation =>
                    conversation.CompanyId == companyId &&
                    (conversation.UserId1 == userId || conversation.UserId2 == userId) &&
                    (includeArchived || !conversation.IsArchived(userId)))
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToArray());

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeChatNotifier : IChatNotifier
    {
        public List<(ChatMessageDto Message, int UnreadCount)> Sent { get; } = [];
        public List<(Guid CompanyId, Guid ReaderId, Guid SenderId)> Read { get; } = [];

        public Task MessageSentAsync(ChatMessageDto message, int unreadCount, CancellationToken ct = default)
        {
            Sent.Add((message, unreadCount));
            return Task.CompletedTask;
        }

        public Task MessagesReadAsync(
            Guid companyId, Guid readerId, Guid senderId, CancellationToken ct = default)
        {
            Read.Add((companyId, readerId, senderId));
            return Task.CompletedTask;
        }

        public Task UserTypingAsync(Guid companyId, Guid senderId, Guid recipientId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
