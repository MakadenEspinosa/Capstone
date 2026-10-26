using ESEvents.Domain.Users;
using ESEvents.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ESEvents.Web.Controllers;

[Route("ui/auth")]
public sealed class UiAuthController(
    UserManager<ApplicationIdentityUser> users,
    SignInManager<ApplicationIdentityUser> signInManager) : Controller
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromForm] LoginForm form)
    {
        var user = await users.FindByEmailAsync(form.Email.Trim());
        if (user is null || user.Status != UserStatus.Active)
            return Redirect("/ui/login?error=1");

        var result = await signInManager.PasswordSignInAsync(
            user,
            form.Password,
            form.RememberMe,
            lockoutOnFailure: true);

        if (!result.Succeeded)
            return Redirect("/ui/login?error=1");

        return Url.IsLocalUrl(form.ReturnUrl)
            ? LocalRedirect(form.ReturnUrl!)
            : Redirect("/ui/user");
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return Redirect("/ui/login");
    }

    public sealed class LoginForm
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
        public string? ReturnUrl { get; set; }
    }
}