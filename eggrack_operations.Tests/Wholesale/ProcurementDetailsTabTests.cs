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

    [Theory]
    [InlineData(nameof(ProcurementController.ReviewPlanQuote),"plans/{planId:long}/quote/department-review")]
    [InlineData(nameof(ProcurementController.DepartmentRejectPlanQuote),"plans/{planId:long}/quote/department-reject")]
    [InlineData(nameof(ProcurementController.FinalRejectPlanQuote),"plans/{planId:long}/quote/final-reject")]
    [InlineData(nameof(ProcurementController.SendPlanMail),"plans/{planId:long}/mail-tasks/{mailTaskId:long}/send")]
    public void ClosedLoopActionsUsePlanScopedRoutes(string methodName,string routeTemplate)
    {
        var method=typeof(ProcurementController).GetMethod(methodName)
            ?? throw new InvalidOperationException(methodName);
        var route=Assert.Single(method.GetCustomAttributes<HttpPostAttribute>());

        Assert.Equal(routeTemplate,route.Template);
    }
}
