using System.Security.Claims;
using ESEvents.Application.Events;
using ESEvents.Domain.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ESEvents.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/events")]
public sealed class EventsController(IEventService events) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CalendarEventDto>>> GetCalendarEvents(
        [FromQuery] DateTimeOffset start,
        [FromQuery] DateTimeOffset end,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await events.GetCalendarEventsAsync(userId, IsAdministrator, start, end, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CalendarEventDto>> Get(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await events.GetAsync(id, userId, IsAdministrator, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CalendarEventDto>> Create(CreateEventRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var created = await events.CreateAsync(userId, IsAdministrator, request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CalendarEventDto>> Update(Guid id, UpdateEventRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var updated = await events.UpdateAsync(id, userId, IsAdministrator, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return await events.DeleteAsync(id, userId, IsAdministrator, ct) ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/participants/{participantUserId:guid}")]
    public async Task<ActionResult<CalendarEventDto>> AddParticipant(
        Guid id,
        Guid participantUserId,
        UpdateEventParticipantRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var updated = await events.AddParticipantAsync(id, userId, IsAdministrator, participantUserId, request.Role, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPut("{id:guid}/participants/{participantUserId:guid}")]
    public async Task<ActionResult<CalendarEventDto>> ChangeParticipantRole(
        Guid id,
        Guid participantUserId,
        UpdateEventParticipantRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var updated = await events.ChangeParticipantRoleAsync(id, userId, IsAdministrator, participantUserId, request.Role, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}/participants/{participantUserId:guid}")]
    public async Task<ActionResult<CalendarEventDto>> RemoveParticipant(Guid id, Guid participantUserId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var updated = await events.RemoveParticipantAsync(id, userId, IsAdministrator, participantUserId, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    private bool IsAdministrator => User.HasClaim("system_role", "Administrator");

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
