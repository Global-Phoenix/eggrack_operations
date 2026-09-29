using eggrack_operations.Models;
using Eggrack.Operations.Infrastructure.Modules.Security.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace eggrack_operations.Controllers;

[Route("account")]
public sealed class AccountController(
    SignInManager<InternalIdentityUser> signInManager,
    UserManager<InternalIdentityUser> userManager) : Controller
{
    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return LocalRedirect(SafeReturnUrl(returnUrl));

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var login = model.UserName.Trim();
        var user = login.Contains('@')
            ? await userManager.FindByEmailAsync(login)
            : await userManager.FindByNameAsync(login);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "邮箱、用户名或密码不正确");
            return View(model);
        }

        var result = await signInManager.CheckPasswordSignInAsync(
            user, model.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "登录失败次数过多，账号已临时锁定");
            return View(model);
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "邮箱、用户名或密码不正确");
            return View(model);
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return LocalRedirect(SafeReturnUrl(model.ReturnUrl));
    }

    [Authorize]
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        return View(new AccountProfileViewModel(
            user.UserName ?? user.StaffRef,
            user.Email,
            user.StaffRef,
            (await userManager.GetRolesAsync(user)).ToArray()));
    }

    [Authorize]
    [ValidateAntiForgeryToken]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet("denied")]
    public IActionResult Denied() => View();

    private string SafeReturnUrl(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Content("~/");
}
