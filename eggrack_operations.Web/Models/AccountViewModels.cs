using System.ComponentModel.DataAnnotations;

namespace eggrack_operations.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "请输入邮箱或用户名")]
    [Display(Name = "邮箱或用户名")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入密码")]
    [DataType(DataType.Password)]
    [Display(Name = "密码")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public sealed record AccountProfileViewModel(
    string UserName,
    string? Email,
    string StaffRef,
    IReadOnlyList<string> Roles);
