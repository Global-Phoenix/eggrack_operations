using System.Reflection;
using eggrack_operations.Areas.Security;
using eggrack_operations.Areas.Wholesale.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Eggrack.Operations.Tests.Wholesale;

public sealed class ProcurementAuthorizationTests
{
    [Fact]
    public void ControllerRequiresPurchasePlanViewPermission()
    {
        var permission = Assert.Single(typeof(ProcurementController)
            .GetCustomAttributes<InternalPermissionAttribute>());
        Assert.NotNull(permission.Arguments);
        Assert.Equal("wholesale.purchase-plan.view", Assert.Single(permission.Arguments!));
    }

    [Theory]
    [InlineData(nameof(ProcurementController.Create), "wholesale.purchase-plan.create")]
    [InlineData(nameof(ProcurementController.Update), "wholesale.purchase-plan.update")]
    [InlineData(nameof(ProcurementController.SaveCosts), "wholesale.purchase-cost.manage")]
    [InlineData(nameof(ProcurementController.Pricing), "wholesale.purchase-cost.manage")]
    [InlineData(nameof(ProcurementController.CreateSupplier), "wholesale.procurement.manage")]
    [InlineData(nameof(ProcurementController.RecordInquiry), "wholesale.procurement.manage")]
    [InlineData(nameof(ProcurementController.RecordSample), "wholesale.procurement.manage")]
    [InlineData(nameof(ProcurementController.SaveCandidate), "wholesale.procurement.manage")]
    [InlineData(nameof(ProcurementController.SaveInquiry), "wholesale.procurement.manage")]
    [InlineData(nameof(ProcurementController.SaveSample), "wholesale.procurement.manage")]
    [InlineData(nameof(ProcurementController.Approve), "wholesale.purchase-quote.approve")]
    [InlineData(nameof(ProcurementController.Reject), "wholesale.purchase-quote.approve")]
    [InlineData(nameof(ProcurementController.IssueProformaInvoice), "wholesale.purchase-quote.approve")]
    [InlineData(nameof(ProcurementController.CompletePlan), "wholesale.procurement.manage")]
    [InlineData(nameof(ProcurementController.UpdateProformaInvoicePricing), "wholesale.purchase-quote.approve")]
    public void WriteActionsRequireSpecificPermission(string methodName,string expectedPermission)
    {
        var method=typeof(ProcurementController).GetMethod(methodName)
            ?? throw new InvalidOperationException(methodName);
        Assert.NotEmpty(method.GetCustomAttributes<HttpPostAttribute>());
        var permission=Assert.Single(method.GetCustomAttributes<InternalPermissionAttribute>());
        Assert.NotNull(permission.Arguments);
        Assert.Equal(expectedPermission,Assert.Single(permission.Arguments!));
    }
}