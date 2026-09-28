using System.ComponentModel.DataAnnotations;

namespace eggrack_operations.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "请输入用户名")]
    [Display(Name = "用户名")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入密码")]
    [DataType(DataType.Password)]
    [Display(Name = "密码")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public sealed class TwoFactorLoginViewModel
{
    [Required(ErrorMessage = "请输入验证码")]
    [StringLength(7, MinimumLength = 6, ErrorMessage = "验证码格式不正确")]
    [Display(Name = "动态验证码")]
    public string Code { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public sealed class EnableAuthenticatorViewModel
{
    public required string SharedKey { get; init; }
    public required string AuthenticatorUri { get; init; }

    [Required(ErrorMessage = "请输入验证码")]
    [StringLength(7, MinimumLength = 6, ErrorMessage = "验证码格式不正确")]
    [Display(Name = "动态验证码")]
    public string Code { get; set; } = string.Empty;
}

public sealed class RecoveryCodesViewModel
{
    public required IReadOnlyCollection<string> Codes { get; init; }
}

public sealed record AccountProfileViewModel(
    string UserName,
    string? Email,
    string StaffRef,
    bool TwoFactorEnabled,
    IReadOnlyList<string> Roles);