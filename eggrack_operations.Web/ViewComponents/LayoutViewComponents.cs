using Eggrack.Operations.Application.Navigation;
using Eggrack.Operations.Application.Modules.Security;
using eggrack_operations.Areas.Security;
using Microsoft.AspNetCore.Mvc;

namespace eggrack_operations.ViewComponents;

public sealed record NavigationViewModel(
    IReadOnlyList<NavigationItem> Items,
    string? CurrentController,
    string? CurrentAction,
    string DisplayName)
{
    public bool IsActive(NavigationItem item)
    {
        if(!string.Equals(item.Controller,CurrentController,StringComparison.OrdinalIgnoreCase))return false;
        if(string.IsNullOrEmpty(item.Action)||string.Equals(item.Action,CurrentAction,StringComparison.OrdinalIgnoreCase))return true;
        return item.Code switch
        {
            "wholesale.purchase-requests" => CurrentAction is "RequestDetails" or "CreatePlanPage",
            "wholesale.purchase-plans" => CurrentAction is "Details" or "EditPlanPage" or "ProformaInvoiceDetails",
            "system.staff" => CurrentAction is "StaffDetails",
            _ => false
        };
    }

    public bool IsGroupActive(NavigationItem item) =>
        item.Children?.Any(IsActive) == true;
}

public sealed class NavigationViewComponent(
    NavigationService navigationService,
    CurrentStaffAccessor currentStaff,
    PermissionEvaluator permissionEvaluator) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var authorization=await currentStaff.LoadAsync(HttpContext.RequestAborted);
        bool Allowed(string permission)=>authorization is not null&&permissionEvaluator.Evaluate(authorization,permission).Allowed;
        return View(new NavigationViewModel(
            navigationService.GetMainNavigation(Allowed),
            RouteData.Values["controller"]?.ToString(),
            RouteData.Values["action"]?.ToString(),
            User.Identity?.Name??"用户"));
    }
}

public sealed record TopbarViewModel(string Title,string DisplayName,int MessageCount);

public sealed class TopbarViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string? title = null) => View(new TopbarViewModel(
        title ?? "工作台",
        User.Identity?.IsAuthenticated == true ? User.Identity.Name ?? "用户" : "管理员",
        0));
}

public sealed record WorkspaceTabDefinition(
    string Key,
    string Title,
    string Url,
    bool IsClosable,
    string? Icon = null);

public sealed record WorkspaceTabsViewModel(WorkspaceTabDefinition CurrentTab);

public static class WorkspaceTabResolver
{
    public static WorkspaceTabDefinition Resolve(
        string? area,string controller,string action,string? title,string path,
        string? requestId=null,string? planId=null,string? invoiceId=null)
    {
        if(controller.Equals("Home",StringComparison.OrdinalIgnoreCase)&&action.Equals("Index",StringComparison.OrdinalIgnoreCase))
            return new("overview","概览","/",false,"bi-house-door");

        if(area?.Equals("Wholesale",StringComparison.OrdinalIgnoreCase)==true&&controller.Equals("Procurement",StringComparison.OrdinalIgnoreCase))
        {
            return action switch
            {
                "WholesaleOverview"=>new("wholesale-overview","批发概览",path,true,"bi-grid"),
                "PurchaseRequests"=>new("wholesale-purchase-requests","采购申请",path,true,"bi-file-earmark-text"),
                "RequestDetails"=>new($"wholesale-request-{requestId??"unknown"}",CleanTitle(title,"采购申请详情"),path,true,"bi-file-earmark-text"),
                "Sourcing"=>new("wholesale-purchase-plans","采购计划",path,true,"bi-cart3"),
                "SupplierDirectory"=>new("wholesale-suppliers","供应商",path,true,"bi-building"),
                "Details"=>new($"wholesale-plan-{planId??"unknown"}",CleanTitle(title,"采购计划详情"),path,true,"bi-cart3"),
                "ProformaInvoiceDetails"=>new($"wholesale-pi-{invoiceId??"unknown"}",CleanTitle(title,"PI 详情"),path,true,"bi-receipt"),
                "CreatePlanPage"=>new($"wholesale-plan-create-{requestId??"unknown"}","创建采购计划",path,true,"bi-cart-plus"),
                "EditPlanPage"=>new($"wholesale-plan-edit-{planId??"unknown"}",CleanTitle(title,"更新采购计划"),path,true,"bi-pencil-square"),
                _=>CreateGeneric(area,controller,action,title,path)
            };
        }

        return CreateGeneric(area,controller,action,title,path);
    }

    private static WorkspaceTabDefinition CreateGeneric(string? area,string controller,string action,string? title,string path)
    {
        var routeKey=string.Join('-',new[]{area,controller,action}
            .Where(value=>!string.IsNullOrWhiteSpace(value))
            .Select(value=>value!.ToLowerInvariant()));
        return new(routeKey,CleanTitle(title,"工作台"),path,true);
    }

    private static string CleanTitle(string? title,string fallback)
    {
        if(string.IsNullOrWhiteSpace(title))return fallback;
        return title.EndsWith(" 详情",StringComparison.Ordinal)?title[..^3]:title;
    }
}

public sealed class WorkspaceTabsViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string? title = null)
    {
        var tab=WorkspaceTabResolver.Resolve(
            RouteData.Values["area"]?.ToString(),
            RouteData.Values["controller"]?.ToString()??"Home",
            RouteData.Values["action"]?.ToString()??"Index",
            title,
            $"{Request.PathBase}{Request.Path}{Request.QueryString}",
            RouteData.Values["requestId"]?.ToString(),
            RouteData.Values["planId"]?.ToString(),
            RouteData.Values["invoiceId"]?.ToString());
        return View(new WorkspaceTabsViewModel(tab));
    }
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
