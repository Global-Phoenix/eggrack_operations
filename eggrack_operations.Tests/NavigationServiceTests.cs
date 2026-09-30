using Eggrack.Operations.Application.Navigation;
using eggrack_operations.ViewComponents;

namespace eggrack_operations.Tests;

public class NavigationServiceTests
{
    [Fact]
    public void Main_navigation_contains_only_implemented_modules()
    {
        var items = new NavigationService().GetMainNavigation();
        var codes = items.SelectMany(item => item.Children ?? [item]).Select(item => item.Code).ToHashSet();

        Assert.Contains("files", codes);
        Assert.Contains("wholesale.overview", codes);
        Assert.Contains("wholesale.purchase-requests", codes);
        Assert.Contains("wholesale.purchase-plans", codes);
        Assert.DoesNotContain("wholesale.sourcing", codes);
        Assert.Contains("wholesale.suppliers", codes);
        Assert.Contains("system.staff", codes);
        Assert.Contains("system.departments", codes);
        Assert.Contains("system.roles", codes);
        Assert.Contains("system.audit", codes);
        Assert.DoesNotContain("tasks", codes);
        Assert.DoesNotContain("logs", codes);
        Assert.DoesNotContain("system.menus", codes);
        Assert.DoesNotContain("system.email-templates", codes);
    }

    [Fact]
    public void Permission_filter_removes_denied_items_and_empty_groups()
    {
        var items=new NavigationService().GetMainNavigation(permission=>permission=="files.view");
        var codes=items.SelectMany(item=>item.Children??[item]).Select(item=>item.Code).ToHashSet();

        Assert.Contains("dashboard",codes);
        Assert.Contains("files",codes);
        Assert.DoesNotContain("wholesale.purchase-requests",codes);
        Assert.DoesNotContain("system.staff",codes);
        Assert.DoesNotContain(items,item=>item.Code=="wholesale");
        Assert.DoesNotContain(items,item=>item.Code=="system");
    }

    [Theory]
    [InlineData("RequestDetails","wholesale.purchase-requests")]
    [InlineData("CreatePlanPage","wholesale.purchase-requests")]
    [InlineData("Details","wholesale.purchase-plans")]
    [InlineData("EditPlanPage","wholesale.purchase-plans")]
    [InlineData("ProformaInvoiceDetails","wholesale.purchase-plans")]
    public void Detail_actions_keep_their_parent_navigation_active(string action,string expectedCode)
    {
        var items=new NavigationService().GetMainNavigation();
        var target=items.SelectMany(item=>item.Children??[item]).Single(item=>item.Code==expectedCode);
        var model=new NavigationViewModel(items,"Procurement",action,"Gavin");

        Assert.True(model.IsActive(target));
    }

    [Fact]
    public void Workspace_overview_is_fixed_and_not_closable()
    {
        var tab=WorkspaceTabResolver.Resolve(null,"Home","Index","概览","/");

        Assert.Equal("overview",tab.Key);
        Assert.Equal("概览",tab.Title);
        Assert.False(tab.IsClosable);
    }

    [Fact]
    public void Workspace_list_key_is_stable_when_query_changes()
    {
        var first=WorkspaceTabResolver.Resolve("Wholesale","Procurement","PurchaseRequests","采购申请","/wholesale/procurement/requests");
        var filtered=WorkspaceTabResolver.Resolve("Wholesale","Procurement","PurchaseRequests","采购申请","/wholesale/procurement/requests?status=submitted");

        Assert.Equal(first.Key,filtered.Key);
        Assert.NotEqual(first.Url,filtered.Url);
    }

    [Theory]
    [InlineData("RequestDetails","42",null,"PR-260929-001 详情","wholesale-request-42","PR-260929-001")]
    [InlineData("Details",null,"88","PP-260929-001 详情","wholesale-plan-88","PP-260929-001")]
    public void Workspace_detail_tabs_use_business_route_id_and_number(
        string action,string? requestId,string? planId,string title,string expectedKey,string expectedTitle)
    {
        var tab=WorkspaceTabResolver.Resolve(
            "Wholesale","Procurement",action,title,"/details",requestId,planId);

        Assert.Equal(expectedKey,tab.Key);
        Assert.Equal(expectedTitle,tab.Title);
        Assert.True(tab.IsClosable);
    }

    [Fact]
    public void Workspace_pi_detail_uses_invoice_identity()
    {
        var tab=WorkspaceTabResolver.Resolve(
            "Wholesale","Procurement","ProformaInvoiceDetails","PI-260929-001 详情","/pi/9",invoiceId:"9");

        Assert.Equal("wholesale-pi-9",tab.Key);
        Assert.Equal("PI-260929-001",tab.Title);
        Assert.True(tab.IsClosable);
    }

    [Fact]
    public void Staff_detail_keeps_staff_navigation_active()
    {
        var items=new NavigationService().GetMainNavigation();
        var target=items.SelectMany(item=>item.Children??[item]).Single(item=>item.Code=="system.staff");
        var model=new NavigationViewModel(items,"Security","StaffDetails","Gavin");

        Assert.True(model.IsActive(target));
    }
}
