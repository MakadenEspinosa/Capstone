using ESEvents.Application.Users;
using ESEvents.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace ESEvents.Web.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(IUserService users) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<UserDto>> Register(RegisterUserRequest request, CancellationToken ct)
    {
        var user = await users.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDto>> Get(Guid id, CancellationToken ct)
    {
        var user = await users.GetAsync(id, ct);
        return user is null ? NotFound() : user;
    }
}
