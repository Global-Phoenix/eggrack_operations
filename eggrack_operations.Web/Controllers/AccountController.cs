using System.Text;
using System.Text.Encodings.Web;
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

        var user = await userManager.FindByNameAsync(model.UserName.Trim());
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "用户名或密码不正确");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(
            user, model.Password, isPersistent: false, lockoutOnFailure: true);

        if (result.RequiresTwoFactor)
            return RedirectToAction(nameof(LoginWithTwoFactor), new { model.ReturnUrl });

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "登录失败次数过多，账号已临时锁定");
            return View(model);
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "用户名或密码不正确");
            return View(model);
        }

        if (user.MustEnableTwoFactor || !await userManager.GetTwoFactorEnabledAsync(user))
            return RedirectToAction(nameof(EnableAuthenticator));

        return LocalRedirect(SafeReturnUrl(model.ReturnUrl));
    }

    [AllowAnonymous]
    [HttpGet("two-factor")]
    public async Task<IActionResult> LoginWithTwoFactor(string? returnUrl = null)
    {
        if (await signInManager.GetTwoFactorAuthenticationUserAsync() is null)
            return RedirectToAction(nameof(Login));

        return View(new TwoFactorLoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [HttpPost("two-factor")]
    public async Task<IActionResult> LoginWithTwoFactor(TwoFactorLoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var code = model.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        var result = await signInManager.TwoFactorAuthenticatorSignInAsync(
            code, isPersistent: false, rememberClient: false);

        if (result.Succeeded)
            return LocalRedirect(SafeReturnUrl(model.ReturnUrl));

        if (result.IsLockedOut)
            ModelState.AddModelError(string.Empty, "验证码错误次数过多，账号已临时锁定");
        else
            ModelState.AddModelError(string.Empty, "动态验证码无效");

        return View(model);
    }

    [Authorize]
    [HttpGet("enable-authenticator")]
    public async Task<IActionResult> EnableAuthenticator()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            key = await userManager.GetAuthenticatorKeyAsync(user);
        }

        return View(CreateAuthenticatorModel(user, key!));
    }

    [Authorize]
    [ValidateAntiForgeryToken]
    [HttpPost("enable-authenticator")]
    public async Task<IActionResult> EnableAuthenticator(EnableAuthenticatorViewModel model)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key))
            return RedirectToAction(nameof(EnableAuthenticator));

        var code = model.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        var valid = await userManager.VerifyTwoFactorTokenAsync(
            user, userManager.Options.Tokens.AuthenticatorTokenProvider, code);
        if (!valid)
        {
            ModelState.AddModelError(nameof(model.Code), "动态验证码无效");
            return View(CreateAuthenticatorModel(user, key, model.Code));
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);
        user.MustEnableTwoFactor = false;
        await userManager.UpdateAsync(user);
        await signInManager.RefreshSignInAsync(user);

        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 8);
        return View("RecoveryCodes", new RecoveryCodesViewModel
        {
            Codes = (codes ?? []).ToArray()
        });
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

    private EnableAuthenticatorViewModel CreateAuthenticatorModel(
        InternalIdentityUser user, string key, string code = "")
    {
        var account = user.Email ?? user.UserName ?? user.StaffRef;
        var uri = $"otpauth://totp/{UrlEncoder.Default.Encode("Eggrack Operations")}:{UrlEncoder.Default.Encode(account)}" +
                  $"?secret={key}&issuer={UrlEncoder.Default.Encode("Eggrack Operations")}&digits=6";
        return new EnableAuthenticatorViewModel
        {
            SharedKey = FormatKey(key),
            AuthenticatorUri = uri,
            Code = code
        };
    }

    private string SafeReturnUrl(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Content("~/");

    private static string FormatKey(string key)
    {
        var result = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
        {
            if (result.Length > 0) result.Append(' ');
            result.Append(key.AsSpan(i, Math.Min(4, key.Length - i)));
        }
        return result.ToString().ToLowerInvariant();
    }
}
