using System.Reflection;
using eggrack_operations.Areas.Wholesale.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Eggrack.Operations.Tests.Wholesale;

public sealed class ProcurementDetailsTabTests
{
    [Fact]
    public void DetailsUsesStablePerPlanRoute()
    {
        var method=typeof(ProcurementController).GetMethod(nameof(ProcurementController.Details))
            ?? throw new InvalidOperationException();
        var route=Assert.Single(method.GetCustomAttributes<HttpGetAttribute>());

        Assert.Equal("plans/{planId:long}/details",route.Template);
    }

    [Fact]
    public void BusinessTabsUseOneLazyLoadRoute()
    {
        var method=typeof(ProcurementController).GetMethod(nameof(ProcurementController.PlanTab))
            ?? throw new InvalidOperationException();
        var route=Assert.Single(method.GetCustomAttributes<HttpGetAttribute>());

        Assert.Equal("plans/{planId:long}/tabs/{tab}",route.Template);
    }
}
