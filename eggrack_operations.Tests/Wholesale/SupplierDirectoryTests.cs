using System.Reflection;
using Eggrack.Operations.Application.Navigation;
using eggrack_operations.Areas.Wholesale.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Eggrack.Operations.Tests.Wholesale;

public sealed class SupplierDirectoryTests
{
    [Fact]
    public void WholesaleNavigationContainsSupplierDirectory()
    {
        var wholesale=new NavigationService().GetMainNavigation().Single(item=>item.Code=="wholesale");
        var supplier=wholesale.Children!.Single(item=>item.Code=="wholesale.suppliers");

        Assert.Equal("SupplierDirectory",supplier.Action);
        Assert.Equal("wholesale.purchase-plan.view",supplier.Permission);
    }

    [Fact]
    public void SupplierDirectoryAndOptionsHaveDistinctRoutes()
    {
        var page=typeof(ProcurementController).GetMethod(nameof(ProcurementController.SupplierDirectory))!;
        var options=typeof(ProcurementController).GetMethod(nameof(ProcurementController.Suppliers))!;

        Assert.Equal("suppliers",Assert.Single(page.GetCustomAttributes<HttpGetAttribute>()).Template);
        Assert.Equal("supplier-options",Assert.Single(options.GetCustomAttributes<HttpGetAttribute>()).Template);
    }
}
