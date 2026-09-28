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
            new("wholesale.purchase-plans", "采购开发计划", "bi-clipboard-check", "Wholesale", "PurchasePlans", "wholesale.purchase-plan.view"),
            new("wholesale.orders", "批发订单", "bi-receipt", "Wholesale", "Orders", "wholesale.order.view")
        ]),
        new("files", "文件中心", "bi-folder2-open", "FileCenter", "Index", "files.view", Area: "Files"),
        new("tasks", "任务中心", "bi-list-task", "Tasks", "Index", "tasks.view"),
        new("logs", "日志中心", "bi-journal-text", "Logs", "Index", "logs.view"),
        new("tools", "开发工具", "bi-tools", Children:
        [
            new("tools.ui-kit", "组件示例", "bi-grid-3x3-gap", "UiKit", "Index")
        ]),
        new("system", "系统管理", "bi-gear", Children:
        [
            new("system.users", "用户管理", "bi-people", "Security", "Staff", "auth.staff.read", Area: "Security"),
            new("system.roles", "角色权限", "bi-shield-lock", "Security", "Roles", "auth.role.read", Area: "Security"),
            new("system.menus", "菜单管理", "bi-menu-button-wide", "Menus", "Index", "system.menu.view"),
            new("system.email-templates", "邮件模板", "bi-envelope-paper", "EmailTemplates", "Index", "system.email-template.view")
        ])
    ];

    public IReadOnlyList<NavigationItem> GetMainNavigation() => Items;
}


