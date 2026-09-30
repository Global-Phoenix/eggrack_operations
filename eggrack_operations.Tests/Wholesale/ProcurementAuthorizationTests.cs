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
    [InlineData(nameof(ProcurementController.CreatePlanPage), "wholesale.purchase-plan.create")]
    [InlineData(nameof(ProcurementController.EditPlanPage), "wholesale.purchase-plan.update")]
    public void EditorPagesRequireSpecificPermission(string methodName,string expectedPermission)
    {
        var method=typeof(ProcurementController).GetMethod(methodName)
            ?? throw new InvalidOperationException(methodName);
        Assert.NotEmpty(method.GetCustomAttributes<HttpGetAttribute>());
        var permission=Assert.Single(method.GetCustomAttributes<InternalPermissionAttribute>());
        Assert.Equal(expectedPermission,Assert.Single(permission.Arguments!));
    }

    [Theory]
    [InlineData(nameof(ProcurementController.Create), "wholesale.purchase-plan.create")]
    [InlineData(nameof(ProcurementController.Update), "wholesale.purchase-plan.update")]
    [InlineData(nameof(ProcurementController.CreatePlanSupplier), "wholesale.procurement.execute")]
    [InlineData(nameof(ProcurementController.SavePlanInquiry), "wholesale.procurement.execute")]
    [InlineData(nameof(ProcurementController.SelectPlanInquiry), "wholesale.procurement.execute")]
    [InlineData(nameof(ProcurementController.SavePlanSample), "wholesale.procurement.execute")]
    [InlineData(nameof(ProcurementController.SavePlanItemProcurement), "wholesale.procurement.execute")]
    [InlineData(nameof(ProcurementController.SavePlanCostQuote), "wholesale.purchase-cost.edit")]
    [InlineData(nameof(ProcurementController.SubmitPlanQuote), "wholesale.purchase-quote.submit")]
    [InlineData(nameof(ProcurementController.BossApprovePlanQuote), "wholesale.purchase-quote.final-approve")]
    [InlineData(nameof(ProcurementController.GeneratePlanPi), "wholesale.purchase-pi.manage")]
    [InlineData(nameof(ProcurementController.SavePlanPi), "wholesale.purchase-pi.manage")]
    [InlineData(nameof(ProcurementController.IssuePlanPi), "wholesale.purchase-pi.issue")]
    [InlineData(nameof(ProcurementController.CreatePlanPiRevision), "wholesale.purchase-pi.manage")]
    [InlineData(nameof(ProcurementController.CancelPlanPi), "wholesale.purchase-pi.manage")]
    [InlineData(nameof(ProcurementController.CompletePurchasePlan), "wholesale.procurement.execute")]
    [InlineData(nameof(ProcurementController.UploadPlanFile), "wholesale.purchase-document.internal")]
    [InlineData(nameof(ProcurementController.SetPlanFileVisibility), "wholesale.purchase-document.internal")]
    public void WriteActionsRequireSpecificPermission(string methodName,string expectedPermission)
    {
        var method=typeof(ProcurementController).GetMethod(methodName)
            ?? throw new InvalidOperationException(methodName);
        Assert.NotEmpty(method.GetCustomAttributes<HttpPostAttribute>());
        var permission=Assert.Single(method.GetCustomAttributes<InternalPermissionAttribute>());
        Assert.NotNull(permission.Arguments);
        Assert.Equal(expectedPermission,Assert.Single(permission.Arguments!));
        Assert.NotEmpty(method.GetCustomAttributes<ValidateAntiForgeryTokenAttribute>());
    }

    [Theory]
    [InlineData("Suppliers")]
    [InlineData("CreateSupplier")]
    [InlineData("SaveInquiry")]
    [InlineData("SaveSample")]
    [InlineData("UploadSampleFile")]
    [InlineData("UpdateProformaInvoicePricing")]
    [InlineData("IssueProformaInvoice")]
    public void RetiredParallelWorkflowActionsAreNotExposed(string methodName)
    {
        Assert.Null(typeof(ProcurementController).GetMethod(methodName));
    }
}
