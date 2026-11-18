using System.Security.Claims;
using ESEvents.Application.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ESEvents.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/chat")]
public sealed class ChatController(IChatService chat) : ControllerBase
{
    [HttpGet("conversations")]
    [ProducesResponseType<IReadOnlyList<ConversationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ConversationDto>>> GetConversations(
        [FromQuery] Guid companyId,
        [FromQuery] bool includeArchived = false,
        [FromQuery] int pageSize = 20,
        [FromQuery] int pageNumber = 1,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await chat.GetConversationsAsync(companyId, userId, includeArchived, pageSize, pageNumber, ct));
    }

    [HttpPost("conversations")]
    [ProducesResponseType<ConversationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ConversationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ConversationDto>> CreateConversation(
        CreateConversationRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var result = await chat.CreateConversationAsync(
            request.CompanyId, userId, request.OtherUserId, ct);
        if (!result.Created) return Ok(result.Conversation);

        return CreatedAtAction(
            nameof(GetConversation),
            new { otherUserId = request.OtherUserId, companyId = request.CompanyId },
            result.Conversation);
    }

    [HttpGet("conversations/{otherUserId:guid}")]
    [ProducesResponseType<GetConversationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetConversationResponse>> GetConversation(
        Guid otherUserId,
        [FromQuery] Guid companyId,
        [FromQuery] int pageSize = 50,
        [FromQuery] long? pageToken = null,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await chat.GetConversationAsync(
            companyId, userId, otherUserId, pageSize, pageToken, ct));
    }

    [HttpPost("messages")]
    [ProducesResponseType<ChatMessageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ChatMessageDto>> SendMessage(
        SendMessageRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var message = await chat.SendMessageAsync(
            request.CompanyId, userId, request.ReceiverId, request.Content, ct);
        return Created($"/api/chat/conversations/{request.ReceiverId}?companyId={request.CompanyId}", message);
    }

    [HttpPost("conversations/{otherUserId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(
        Guid otherUserId, [FromQuery] Guid companyId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await chat.MarkAsReadAsync(companyId, userId, otherUserId, ct);
        return NoContent();
    }

    [HttpGet("unread-count")]
    [ProducesResponseType<int>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<int>> GetUnreadCount([FromQuery] Guid companyId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await chat.GetUnreadCountAsync(companyId, userId, cancellationToken: ct));
    }

    [HttpGet("unread-summary")]
    [ProducesResponseType<UnreadSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UnreadSummary>> GetUnreadSummary(
        [FromQuery] Guid companyId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await chat.GetUnreadSummaryAsync(companyId, userId, ct));
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
