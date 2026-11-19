using System.Security.Claims;
using ESEvents.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ESEvents.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/events/export")]
public sealed class EventExportsController(IEventExportService exports) : ControllerBase
{
    /// <summary>Download calendar events visible to the current user as a Word or Excel report.</summary>
    /// <remarks>Dates are inclusive calendar dates in the supplied time zone.</remarks>
    [HttpGet("{format}")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Download(
        string format, [FromQuery] DateOnly start, [FromQuery] DateOnly end,
        [FromQuery] string timeZone, [FromQuery] bool includeParticipants, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();
        if (start == default || end == default)
            return BadRequest(new ProblemDetails { Status = 400, Title = "Start and end dates are required." });
        var content = await exports.ExportAsync(userId, User.HasClaim("system_role", "Administrator"),
            start, end, timeZone, includeParticipants, format, ct);
        Response.Headers.CacheControl = "no-store";
        var contentType = format == "docx"
            ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        return File(content, contentType, $"events-{start:yyyy-MM-dd}-to-{end:yyyy-MM-dd}.{format}");
    }
}
