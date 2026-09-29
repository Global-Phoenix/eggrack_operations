namespace Eggrack.Operations.Application.Navigation;

public sealed record NavigationItem(
    string Code,
    string Title,
    string Icon,
    string? Controller = null,
    string? Action = null,
    string? Permission = null,
    IReadOnlyList<NavigationItem>? Children = null,
    string? Area = null);

public sealed class NavigationService
{
    private static readonly IReadOnlyList<NavigationItem> Items =
    [
        new("dashboard", "工作台", "bi-speedometer2", "Home", "Index"),
        new("wholesale", "批发管理", "bi-cart3", Children:
        [
            new("wholesale.purchase-plans", "采购开发", "bi-clipboard-check", "Procurement", "Index", "wholesale.purchase-plan.view", Area: "Wholesale")
        ]),
        new("files", "文件中心", "bi-folder2-open", "FileCenter", "Index", "files.view", Area: "Files"),
        new("system", "系统管理", "bi-gear", Children:
        [
            new("system.users", "用户管理", "bi-people", "Security", "Staff", "auth.staff.read", Area: "Security"),
            new("system.roles", "角色权限", "bi-shield-lock", "Security", "Roles", "auth.role.read", Area: "Security")
        ])
    ];

    public IReadOnlyList<NavigationItem> GetMainNavigation() => Items;
}


