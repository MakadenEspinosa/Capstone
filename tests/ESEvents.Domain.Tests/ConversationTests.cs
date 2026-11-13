using ESEvents.Domain.Chat;

namespace ESEvents.Domain.Tests;

public class ConversationTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private readonly Guid User1Id = Guid.NewGuid();
    private readonly Guid User2Id = Guid.NewGuid();

    [Fact]
    public void Create_NormalizesUserIds_LowerIdFirst()
    {
        var conversation = Conversation.Create(CompanyId, User1Id, User2Id);

        if (User1Id < User2Id)
        {
            Assert.Equal(User1Id, conversation.UserId1);
            Assert.Equal(User2Id, conversation.UserId2);
        }
        else
        {
            Assert.Equal(User2Id, conversation.UserId1);
            Assert.Equal(User1Id, conversation.UserId2);
        }
    }

    [Fact]
    public void Create_NormalizesUserIds_Reversed()
    {
        var conversation = Conversation.Create(CompanyId, User2Id, User1Id);

        if (User1Id < User2Id)
        {
            Assert.Equal(User1Id, conversation.UserId1);
            Assert.Equal(User2Id, conversation.UserId2);
        }
        else
        {
            Assert.Equal(User2Id, conversation.UserId1);
            Assert.Equal(User1Id, conversation.UserId2);
        }
    }

    [Fact]
    public void Create_WithEmptyCompanyId_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Conversation.Create(Guid.Empty, User1Id, User2Id));
        Assert.Contains("Company ID cannot be empty", ex.Message);
    }

    [Fact]
    public void Create_WithEmptyUserId1_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Conversation.Create(CompanyId, Guid.Empty, User2Id));
        Assert.Contains("User 1 ID cannot be empty", ex.Message);
    }

    [Fact]
    public void Create_WithEmptyUserId2_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Conversation.Create(CompanyId, User1Id, Guid.Empty));
        Assert.Contains("User 2 ID cannot be empty", ex.Message);
    }

    [Fact]
    public void Create_WhenUserIdsSame_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Conversation.Create(CompanyId, User1Id, User1Id));
        Assert.Contains("Cannot have conversation with yourself", ex.Message);
    }

    [Fact]
    public void AddMessage_IncreasesUnreadCountForReceiver()
    {
        var conversation = Conversation.Create(CompanyId, User1Id, User2Id);
        var message = ChatMessage.Create(CompanyId, User1Id, User2Id, "Hello");

        // Determine normalized IDs
        var normalizedUser1 = User1Id < User2Id ? User1Id : User2Id;
        var normalizedUser2 = User1Id < User2Id ? User2Id : User1Id;

        // When User1 sends to User2, User2's unread count increases
        conversation.AddMessage(message.MessageTimestamp, User1Id);

        if (User1Id == normalizedUser1)
        {
            Assert.Equal(1, conversation.UnreadCountUser2);
            Assert.Equal(0, conversation.UnreadCountUser1);
        }
        else
        {
            Assert.Equal(1, conversation.UnreadCountUser1);
            Assert.Equal(0, conversation.UnreadCountUser2);
        }
    }

    [Fact]
    public void AddMessage_MultipleMessages_TracksCount()
    {
        var conversation = Conversation.Create(CompanyId, User1Id, User2Id);

        for (int i = 0; i < 5; i++)
        {
            conversation.AddMessage(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + i, User1Id);
        }

        var normalizedUser1 = User1Id < User2Id ? User1Id : User2Id;
        if (User1Id == normalizedUser1)
        {
            Assert.Equal(5, conversation.UnreadCountUser2);
        }
        else
        {
            Assert.Equal(5, conversation.UnreadCountUser1);
        }
    }

    [Fact]
    public void MarkAsRead_ClearsUnreadCount()
    {
        var conversation = Conversation.Create(CompanyId, User1Id, User2Id);
        
        var normalizedUser1 = User1Id < User2Id ? User1Id : User2Id;
        var normalizedUser2 = User1Id < User2Id ? User2Id : User1Id;

        // Add messages from normalized User1 to normalized User2
        conversation.AddMessage(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), normalizedUser1);
        conversation.AddMessage(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 1, normalizedUser1);

        // Mark as read for normalized User2
        conversation.MarkAsRead(normalizedUser2);

        Assert.Equal(0, conversation.UnreadCountUser2);
    }

    [Fact]
    public void IsArchived_DefaultsFalse()
    {
        var conversation = Conversation.Create(CompanyId, User1Id, User2Id);

        var normalizedUser1 = User1Id < User2Id ? User1Id : User2Id;
        Assert.False(conversation.IsArchived(normalizedUser1));
    }

    [Fact]
    public void Archive_MarksAsArchivedForUser()
    {
        var conversation = Conversation.Create(CompanyId, User1Id, User2Id);

        var normalizedUser1 = User1Id < User2Id ? User1Id : User2Id;
        conversation.Archive(normalizedUser1);

        Assert.True(conversation.IsArchived(normalizedUser1));
    }

    [Fact]
    public void Unarchive_RemovesArchiveFlag()
    {
        var conversation = Conversation.Create(CompanyId, User1Id, User2Id);

        var normalizedUser1 = User1Id < User2Id ? User1Id : User2Id;
        conversation.Archive(normalizedUser1);
        Assert.True(conversation.IsArchived(normalizedUser1));

        conversation.Unarchive(normalizedUser1);
        Assert.False(conversation.IsArchived(normalizedUser1));
    }

    [Fact]
    public void LastMessageTimestamp_UpdatesOnAddMessage()
    {
        var conversation = Conversation.Create(CompanyId, User1Id, User2Id);
        Assert.Null(conversation.LastMessageTimestamp);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        conversation.AddMessage(timestamp, User1Id);

        Assert.Equal(timestamp, conversation.LastMessageTimestamp);
    }

    [Fact]
    public void Archive_IsPerUser()
    {
        var conversation = Conversation.Create(CompanyId, User1Id, User2Id);

        var normalizedUser1 = User1Id < User2Id ? User1Id : User2Id;
        var normalizedUser2 = User1Id < User2Id ? User2Id : User1Id;

        conversation.Archive(normalizedUser1);

        Assert.True(conversation.IsArchived(normalizedUser1));
        Assert.False(conversation.IsArchived(normalizedUser2));
    }
}
