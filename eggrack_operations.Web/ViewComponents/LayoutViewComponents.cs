using Eggrack.Operations.Application.Navigation;
using Microsoft.AspNetCore.Mvc;

namespace eggrack_operations.ViewComponents;

public sealed record NavigationViewModel(
    IReadOnlyList<NavigationItem> Items,
    string? CurrentController,
    string? CurrentAction)
{
    public bool IsActive(NavigationItem item) =>
        string.Equals(item.Controller, CurrentController, StringComparison.OrdinalIgnoreCase) &&
        (string.IsNullOrEmpty(item.Action) ||
         string.Equals(item.Action, CurrentAction, StringComparison.OrdinalIgnoreCase));

    public bool IsGroupActive(NavigationItem item) =>
        item.Children?.Any(IsActive) == true;
}

public sealed class NavigationViewComponent(NavigationService navigationService) : ViewComponent
{
    public IViewComponentResult Invoke() => View(new NavigationViewModel(
        navigationService.GetMainNavigation(),
        RouteData.Values["controller"]?.ToString(),
        RouteData.Values["action"]?.ToString()));
}

public sealed record TopbarViewModel(string Title, string CurrentPath, string DisplayName, int TaskCount, int MessageCount);

public sealed class TopbarViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string? title = null) => View(new TopbarViewModel(
        title ?? "工作台",
        HttpContext.Request.Path.Value ?? "/",
        User.Identity?.IsAuthenticated == true ? User.Identity.Name ?? "用户" : "管理员",
        0,
        0));
}

public sealed class BreadcrumbViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string? title = null) => View("Default", title ?? "工作台");
}

public sealed record ModuleCard(string Code, string Title, string Description, string Theme, string Url, string Icon);

public sealed class ModuleGridViewComponent : ViewComponent
{
    private static readonly IReadOnlyList<ModuleCard> Modules =
    [
        new("批", "批发管理", "采购申请与采购计划", "orange", "/wholesale/procurement", "bi-cart3"),
        new("权", "权限管理", "用户、角色、权限", "blue", "/security", "bi-shield-lock"),
        new("文", "文件中心", "上传、下载、归档", "green", "/files", "bi-folder2-open")
    ];

    public IViewComponentResult Invoke() => View(Modules);
}




public sealed record PageHeaderViewModel(string Title,string? Subtitle,string? ParentTitle,string? ParentUrl,string? StatusText,string StatusTone,string? PrimaryText,string? PrimaryUrl,string? PrimaryModalTarget,string? SecondaryText,string? SecondaryUrl,string? TertiaryText,string? TertiaryUrl);
public sealed class PageHeaderViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string title,string? subtitle=null,string? parentTitle=null,string? parentUrl=null,string? statusText=null,string statusTone="neutral",string? primaryText=null,string? primaryUrl=null,string? primaryModalTarget=null,string? secondaryText=null,string? secondaryUrl=null,string? tertiaryText=null,string? tertiaryUrl=null) =>
        View(new PageHeaderViewModel(title,subtitle,parentTitle,parentUrl,statusText,statusTone,primaryText,primaryUrl,primaryModalTarget,secondaryText,secondaryUrl,tertiaryText,tertiaryUrl));
}
public sealed record EmptyStateViewModel(string Title,string? Description,string Icon,string? ActionText,string? ActionUrl);
public sealed class EmptyStateViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string title,string? description=null,string icon="bi-inbox",string? actionText=null,string? actionUrl=null) =>
        View(new EmptyStateViewModel(title,description,icon,actionText,actionUrl));
}