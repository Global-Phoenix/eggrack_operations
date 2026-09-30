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
        new("dashboard", "概览", "bi-grid-1x2", "Home", "Index"),
        new("wholesale", "批发管理", "bi-cart3", Children:
        [
            new("wholesale.overview", "批发概览", "bi-grid", "Procurement", "WholesaleOverview", "wholesale.purchase-plan.view", Area: "Wholesale"),
            new("wholesale.purchase-requests", "采购申请", "bi-file-earmark-text", "Procurement", "PurchaseRequests", "wholesale.purchase-plan.view", Area: "Wholesale"),
            new("wholesale.purchase-plans", "采购计划", "bi-bag-check", "Procurement", "Sourcing", "wholesale.purchase-plan.view", Area: "Wholesale"),
            new("wholesale.suppliers", "供应商", "bi-building", "Procurement", "SupplierDirectory", "wholesale.purchase-plan.view", Area: "Wholesale")
        ]),
        new("files", "文件管理", "bi-folder2-open", "FileCenter", "Index", "files.view", Area: "Files"),
        new("system", "系统管理", "bi-gear", Children:
        [
            new("system.staff", "人员", "bi-people", "Security", "Staff", "auth.staff.read", Area: "Security"),
            new("system.departments", "部门", "bi-diagram-3", "Security", "Departments", "auth.department.read", Area: "Security"),
            new("system.roles", "角色与权限", "bi-shield-lock", "Security", "Roles", "auth.role.read", Area: "Security"),
            new("system.audit", "审计日志", "bi-journal-text", "Security", "Audit", "auth.audit.read", Area: "Security")
        ])
    ];

    public IReadOnlyList<NavigationItem> GetMainNavigation() => Items;

    public IReadOnlyList<NavigationItem> GetMainNavigation(Func<string,bool> isAllowed) =>
        Items.Select(item=>Filter(item,isAllowed)).Where(item=>item is not null).Cast<NavigationItem>().ToArray();

    private static NavigationItem? Filter(NavigationItem item,Func<string,bool> isAllowed)
    {
        if(!string.IsNullOrWhiteSpace(item.Permission)&&!isAllowed(item.Permission))return null;
        if(item.Children is not { Count: > 0 })return item;
        var children=item.Children.Select(child=>Filter(child,isAllowed)).Where(child=>child is not null).Cast<NavigationItem>().ToArray();
        return children.Length==0?null:item with{Children=children};
    }
}


