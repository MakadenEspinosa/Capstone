using System.Security.Claims;
using ESEvents.Application.Chat;
using ESEvents.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ESEvents.Web.Tests;

public sealed class ChatControllerTests
{
    [Fact]
    public async Task SendMessage_UsesAuthenticatedUserAsSender()
    {
        var companyId = Guid.NewGuid();
        var authenticatedUserId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var message = new ChatMessageDto
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            SenderId = authenticatedUserId,
            ReceiverId = receiverId,
            Content = "Hello"
        };
        var chat = new Mock<IChatService>();
        chat.Setup(service => service.SendMessageAsync(
                companyId, authenticatedUserId, receiverId, "Hello", It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);
        var controller = new ChatController(chat.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, authenticatedUserId.ToString())],
                        "test"))
                }
            }
        };

        var response = await controller.SendMessage(
            new SendMessageRequest
            {
                CompanyId = companyId,
                ReceiverId = receiverId,
                Content = "Hello"
            },
            CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(response.Result);
        Assert.Equal(message, created.Value);
        chat.Verify(service => service.SendMessageAsync(
            companyId, authenticatedUserId, receiverId, "Hello", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
