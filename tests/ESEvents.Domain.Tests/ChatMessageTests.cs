using ESEvents.Domain.Chat;
using ESEvents.Domain.Common;

namespace ESEvents.Domain.Tests;

public class ChatMessageTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SenderId = Guid.NewGuid();
    private static readonly Guid ReceiverId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_Succeeds()
    {
        var message = ChatMessage.Create(CompanyId, SenderId, ReceiverId, "Hello, World!");

        Assert.Equal(CompanyId, message.CompanyId);
        Assert.Equal(SenderId, message.SenderId);
        Assert.Equal(ReceiverId, message.ReceiverId);
        Assert.Equal("Hello, World!", message.Content);
        Assert.False(message.IsRead);
        Assert.Null(message.ReadAtUtc);
        Assert.False(message.IsDeleted);
    }

    [Fact]
    public void Create_WithEmptyCompanyId_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => ChatMessage.Create(Guid.Empty, SenderId, ReceiverId, "Hello"));
        Assert.Contains("Company ID cannot be empty", ex.Message);
    }

    [Fact]
    public void Create_WithEmptySenderId_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => ChatMessage.Create(CompanyId, Guid.Empty, ReceiverId, "Hello"));
        Assert.Contains("Sender ID cannot be empty", ex.Message);
    }

    [Fact]
    public void Create_WithEmptyReceiverId_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => ChatMessage.Create(CompanyId, SenderId, Guid.Empty, "Hello"));
        Assert.Contains("Receiver ID cannot be empty", ex.Message);
    }

    [Fact]
    public void Create_WhenSenderEqualsReceiver_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => ChatMessage.Create(CompanyId, SenderId, SenderId, "Hello"));
        Assert.Contains("Cannot send message to yourself", ex.Message);
    }

    [Fact]
    public void Create_WithEmptyContent_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => ChatMessage.Create(CompanyId, SenderId, ReceiverId, ""));
        Assert.Contains("Message content cannot be empty", ex.Message);
    }

    [Fact]
    public void Create_WithWhitespaceContent_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => ChatMessage.Create(CompanyId, SenderId, ReceiverId, "   "));
        Assert.Contains("Message content cannot be empty", ex.Message);
    }

    [Fact]
    public void Create_WithContentExceedingMaxLength_Throws()
    {
        var longContent = new string('a', 5001);
        var ex = Assert.Throws<ArgumentException>(
            () => ChatMessage.Create(CompanyId, SenderId, ReceiverId, longContent));
        Assert.Contains("Message content cannot exceed 5000 characters", ex.Message);
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        var message = ChatMessage.Create(CompanyId, SenderId, ReceiverId, "  Hello, World!  ");

        Assert.Equal("Hello, World!", message.Content);
    }

    [Fact]
    public void Create_SetsMessageTimestamp()
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var message = ChatMessage.Create(CompanyId, SenderId, ReceiverId, "Test");
        var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Assert.InRange(message.MessageTimestamp, before, after);
    }

    [Fact]
    public void MarkAsRead_WhenUnread_UpdatesState()
    {
        var message = ChatMessage.Create(CompanyId, SenderId, ReceiverId, "Test");

        message.MarkAsRead();

        Assert.True(message.IsRead);
        Assert.NotNull(message.ReadAtUtc);
    }

    [Fact]
    public void MarkAsRead_WhenAlreadyRead_DoesNothing()
    {
        var message = ChatMessage.Create(CompanyId, SenderId, ReceiverId, "Test");
        message.MarkAsRead();
        var firstReadTime = message.ReadAtUtc;

        // Small delay to ensure different timestamp
        System.Threading.Thread.Sleep(10);
        message.MarkAsRead();

        Assert.Equal(firstReadTime, message.ReadAtUtc);
    }

    [Fact]
    public void Delete_WhenNotDeleted_SetsFlag()
    {
        var message = ChatMessage.Create(CompanyId, SenderId, ReceiverId, "Test");

        message.Delete();

        Assert.True(message.IsDeleted);
    }

    [Fact]
    public void Delete_WhenAlreadyDeleted_DoesNothing()
    {
        var message = ChatMessage.Create(CompanyId, SenderId, ReceiverId, "Test");
        message.Delete();

        // Should not throw
        message.Delete();
        Assert.True(message.IsDeleted);
    }

    [Fact]
    public void CreateFromDatabase_WithValidData_Succeeds()
    {
        var messageId = Guid.NewGuid();
        var createdAtUtc = DateTime.UtcNow;

        var message = ChatMessage.CreateFromDatabase(
            messageId, CompanyId, SenderId, ReceiverId,
            "Deserialized message", 1234567890,
            isRead: true, readAtUtc: DateTime.UtcNow.AddHours(-1),
            isDeleted: false, createdAtUtc: createdAtUtc);

        Assert.Equal(messageId, message.Id);
        Assert.Equal(CompanyId, message.CompanyId);
        Assert.Equal(SenderId, message.SenderId);
        Assert.Equal(ReceiverId, message.ReceiverId);
        Assert.Equal("Deserialized message", message.Content);
        Assert.Equal(1234567890, message.MessageTimestamp);
        Assert.True(message.IsRead);
        Assert.NotNull(message.ReadAtUtc);
        Assert.False(message.IsDeleted);
    }

    [Fact]
    public void CreateFromDatabase_WithMaxLengthContent_Succeeds()
    {
        var maxContent = new string('x', 5000);

        var message = ChatMessage.CreateFromDatabase(
            Guid.NewGuid(), CompanyId, SenderId, ReceiverId,
            maxContent, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            false, null, false, DateTime.UtcNow);

        Assert.Equal(5000, message.Content.Length);
    }
}
