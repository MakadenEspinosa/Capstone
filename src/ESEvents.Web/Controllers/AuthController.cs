using ESEvents.Application.Users;
using ESEvents.Domain.Users;
using ESEvents.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ESEvents.Web.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAntiforgery antiforgery,
    UserManager<ApplicationIdentityUser> users,
    SignInManager<ApplicationIdentityUser> signInManager) : ControllerBase
{
    [HttpGet("csrf")]
    [AllowAnonymous]
    public ActionResult<CsrfTokenResponse> GetCsrfToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new CsrfTokenResponse(tokens.RequestToken!));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<UserDto>> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || user.Status != UserStatus.Active)
            return Unauthorized();

        var result = await signInManager.PasswordSignInAsync(
            user,
            request.Password,
            request.RememberMe,
            lockoutOnFailure: true);

        if (!result.Succeeded)
            return Unauthorized();

        return Ok(ToDto(user));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    [HttpGet("session")]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetSession()
    {
        var user = await users.GetUserAsync(User);
        return user is null ? Unauthorized() : Ok(ToDto(user));
    }

    private static UserDto ToDto(ApplicationIdentityUser user) => new(
        user.Id,
        user.Email ?? string.Empty,
        user.FirstName,
        user.LastName,
        user.PhoneNumber,
        user.Bio,
        user.SystemRole,
        user.Status);
}

public sealed record LoginRequest(string Email, string Password, bool RememberMe = false);

public sealed record CsrfTokenResponse(string RequestToken);
