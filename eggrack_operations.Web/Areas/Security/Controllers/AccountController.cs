using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eggrack_operations.Areas.Security.Controllers;

[Area("Security")]
[Route("account")]
public sealed class AccountController : Controller
{
    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        var destination = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
        return Challenge(
            new AuthenticationProperties { RedirectUri = destination },
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    [Authorize]
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public IActionResult Logout() => SignOut(
        new AuthenticationProperties { RedirectUri = "/" },
        CookieAuthenticationDefaults.AuthenticationScheme,
        OpenIdConnectDefaults.AuthenticationScheme);

    [AllowAnonymous]
    [HttpGet("denied")]
    public IActionResult Denied() => View();
}

